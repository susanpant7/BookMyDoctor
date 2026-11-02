# Developer docs: AI and RAG

This page explains how the AI assistant, Vaidya Mitra, works: where its knowledge comes from, how it finds the right part of it for a question, how the prompt is built, how it looks up free times and books appointments, and how its replies are checked. How to run and set up the app is on the [Installation](/docs) tab.

## 1. What RAG is

A chat model only knows what it learned in training. It knows nothing about our hospital: our doctors, their fees, our opening hours or our cancellation policy. We could train it again on our data (fine-tuning), but that is slow and costly, it has to be done again after every change, and the model can still mix up facts.

**RAG (retrieval-augmented generation)** gives the model the facts at the moment it answers instead:

1. **Retrieve:** find the few pieces of our knowledge that are closest to the patient's question.
2. **Augment:** attach them to the question.
3. **Generate:** the model writes the answer from them.

A new doctor or a changed fee is in the next answer, with no training. The model is told to answer only from the attached information, and to say it does not know otherwise.

**Live data is not in the knowledge.** Free times and bookings change every minute, so the model looks them up with **tools** (functions the server runs for it, section 6) instead.

## 2. Vectors and the vector database

To find the knowledge "closest" to a question, we compare meanings, not words. An **embedding model** turns a text into a vector: a list of numbers (768 for `nomic-embed-text`). Texts with a similar meaning get vectors that point in a similar direction, so "How much does the skin doctor charge?" lands near the dermatology doctor's fee section even though the words are different.

The closeness of two vectors is their **cosine similarity**: 1 is the same direction, 0 is unrelated. The app stores each knowledge section with its vector in the `KnowledgeChunks` table of the SQLite database. The **sqlite-vec** extension adds the function `vec_distance_cosine`, so the search is one SQL query inside the same database file. There is no separate vector database to run.

Vectors from two different embedding models cannot be compared. That is why changing the embedding model embeds all the knowledge again (section 10).

## 3. The big picture

```text
                 INGESTION (when knowledge is loaded or changes)
 Markdown file ──► split into "## " sections ──► embedding model ──► KnowledgeChunks (text + vector)
 doctors, specialties, hospital ──► generated sections ──┘

                 ONE CHAT MESSAGE
 patient ──► SignalR hub ──► ChatAssistant
                               1. retrieve: embed the message, search KnowledgeChunks
                               2. prompt:   system prompt + recent history + knowledge on the latest message
                               3. generate: chat model, running tools (free times, booking) until it answers
                               4. check:    fact checker; rewrite once if it made something up
                         ◄── reply (shown while it is written), plus time buttons
```

| Part | Where in the code |
| --- | --- |
| Chat over SignalR | `src/server/Features/Chats/ChatHub.cs` |
| The RAG pipeline | `src/server/Features/Chats/ChatAssistant.cs` |
| Chat models (Ollama, online) | `OllamaChatModel.cs`, `OnlineChatModel.cs`, `IChatModel.cs` |
| Tools | `src/server/Features/Chats/Tools/` |
| Fact check | `src/server/Features/Chats/ReplyFactChecker.cs` |
| Knowledge, embeddings, search | `src/server/Features/Knowledge/` |
| Settings and model switching | `src/server/Features/Settings/` |

## 4. Ingestion: from a file to a vector

### The file format

A knowledge file is Markdown. The `# ` line is the title of the document, and every `## ` line starts a section:

```markdown
# Booking appointments

## How far ahead you can book
Appointments can be booked up to 30 days in advance. The earliest appointment that can be booked online is 2 hours from the current time.

## Appointment length
Appointment length depends on the doctor and is shown on each available slot...
```

**Each section is one chunk**: one piece the search can find. The heading stays in the chunk's text, so the chunk makes sense on its own. A good section is about one topic and names it ("Cancellation policy", not "More info"), because the search only sees that one section.

### Where the knowledge comes from

- **Bundled files** in `src/server/Knowledge/` (booking, cancellations, payments, emergency, FAQ, ...). They are loaded when the first embedding model is selected, or with **Knowledge files** on Settings, Manage App Data.
- **Uploaded and written files** from the Knowledge page.
- **Generated files**, built by the server from the database (`KnowledgeSources.cs`):
  - `doctor-settings.md`: one section per doctor, with the specialty, experience, languages, fees and weekly hours;
  - `specialty-settings.md`: one section per specialty, with its doctors;
  - `hospital-settings.md`: the address and phone, opening hours, parking, facilities and test prices.

  They are kept in sync when a doctor, specialty or the hospital details change. Only new or changed sections are embedded again (each chunk stores a hash of its text), and sections of a deleted doctor are removed.

### Steps

1. The file is split into sections at each `## ` line. Each gets an id such as `booking.md#how-far-ahead-you-can-book`.
2. The **document prefix** is put in front of each text (`search_document:` for `nomic-embed-text`; online models need none). Some embedding models were trained with these prefixes and match better with them.
3. The texts are sent to the embedding model in batches of 32 (`Embedder.cs`). A brief network error, a 429 or a 503 is retried once.
4. All vectors are made first and saved together, so a failure halfway changes nothing. A file with the same name replaces its earlier sections.

## 5. Retrieval: finding the right sections

For each patient message, `KnowledgeRetriever.cs`:

1. **Embeds the latest message only**, with the **query prefix** (`search_query:`). Earlier messages, especially the assistant's long replies, would pull the search back to the previous topic. The model still sees the whole conversation, so it can resolve "how much does that doctor charge?".
2. **Finds the closest sections** with sqlite-vec:

   ```sql
   SELECT Id, Source, Title, Text, vec_distance_cosine(Embedding, @query) AS Distance
   FROM KnowledgeChunks
   ORDER BY Distance
   LIMIT @top_k
   ```

   The score is `1 - Distance`.
3. **Filters** them with two settings:
   - **Minimum similarity** (0.5): sections below it are dropped.
   - **Maximum score gap** (0.05): sections more than this below the best match are dropped too. Scores bunch together, so weak matches would otherwise still pass the minimum.

**Test retrieval on its own.** **Test a question** on the Knowledge page runs exactly this search and shows the sections with their scores, without the chat model. When the assistant answers wrongly, this is the first thing to check: was the right section found?

If the search fails (no embedding model, the service is down), the assistant still answers, without knowledge, and the server log says why.

## 6. Generation: the prompt, the model and the tools

### How the prompt is built

`ChatAssistant.cs` builds the messages for the model:

1. **System message:** the **System prompt** setting, with its placeholders filled in: `{patient_name}`, `{today}`, `{calendar}` (the next 7 days with their dates, so the model never works dates out itself) and `{hospital_phone}`. It says who the assistant is and its rules: answer only from the HOSPITAL INFORMATION, use the tools for free times and bookings, never invent doctors, prices or times, no diagnoses.
2. **Recent history:** the last **Messages of history** (12) messages. It is trimmed in steps of half that size, not one message at a time, so the start of the prompt stays the same for several turns and the model's prompt cache keeps working.
3. **Earlier lookups:** the tool calls and results of the last **Turns that keep lookups** (3) patient messages are put back after those messages (`ToolTranscripts.cs`). The chat history only stores what the patient sees, so without this the model would forget the fees and times it looked up.
4. **The latest message with the knowledge (augment):**

   ```text
   HOSPITAL INFORMATION:
   [1] ## Dermatology ...
   [2] ## Doctor Chhaalarog Bisesagya ...

   PATIENT MESSAGE:
   how much does the skin doctor charge?
   ```

   Only the latest message gets the knowledge, so everything before it stays the same from turn to turn.

### The model

The chat model is chosen on the Settings page (`IChatModel.cs`). An address with no path, such as `http://localhost:11434`, uses Ollama's own API. Any other address is called in the OpenAI format (`/chat/completions`), which Gemini, Groq, Mistral, OpenRouter, Cerebras, xAI and OpenAI all accept. The key is read from the `.env` variable the model names (`GEMINI_API_KEY`, ...). The reply is streamed, and the hub shows it in the chat while it is being written.

### Tools

The model gets three tools. It does not run them: it asks, the server runs them and sends back the result as a `tool` message, and the model continues. This repeats for up to **Lookups per reply** (5) rounds.

| Tool | What it does |
| --- | --- |
| `get_available_slots` | The free times of a doctor on a day. It accepts a doctor's name, part of it or a specialty ("dermatologist"), and a day such as "tuesday" or "tomorrow", because small models are unreliable at dates. If the day is full, it looks ahead for the next free day. The times are shown as buttons under the reply |
| `get_my_appointments` | The patient's own appointments |
| `book_appointment` | Books in two steps, see below and section 7 |

The patient always comes from the signed-in user's token, never from what the model sends. A tool that gets wrong arguments returns the problem as text ("No doctor named ..."), so the model can correct itself.

### Booking safely

Small models sometimes say "you're booked" when nothing was booked. So booking never depends on the model alone:

1. **Propose.** `book_appointment` with a time only checks it is free, remembers it as pending, and returns a summary for the patient to confirm. The server remembers the doctor and day of the times last shown (`BookingDrafts.cs`), so "11:30 please" is enough.
2. **Confirm.** Only a call while answering a **later** patient message books the pending appointment. A real patient reply always sits between proposing and booking.

The time buttons and **Confirm** book directly through the hub, without the model. A plain "yes" straight after a proposal is booked the same way.

## 7. Free times and booking, step by step

This section follows one booking from the first message to the reference number, with what is sent and received at each step. The examples are shortened (ids are cut, and only the fields that matter are shown). Times are in the hospital's time zone (`America/Chicago`); today in the examples is Sunday 1 November 2026.

### The idea in simple words

- **The model never books by itself.** It can only ask the server to run a tool. The server checks everything (is the doctor real, is the time free, may this patient book) and answers in plain text.
- **Free times are calculated, not stored.** A free slot is the doctor's weekly hours, minus days off, minus appointments already booked, minus times less than 2 hours from now. Bookings are allowed up to 30 days ahead (`AvailabilityService.cs`, `BookingRules.cs`).
- **The server remembers the booking in progress** (`BookingDrafts.cs`): the times last shown to the patient (the *offer*) and the time the patient picked (the *pending* booking). The model does not have to remember the doctor, day and time across messages.
- **Booking takes two steps with a patient reply in between:** first the time is proposed, then the patient says yes, and only then is it booked.

```text
patient: "skin rash, free time monday?"
   └─► model calls get_available_slots ─► server: offer saved, times returned ─► buttons in the chat
patient: "11:30 please"
   └─► model calls book_appointment(time) ─► server: time checked, pending saved, "NOT BOOKED YET"
patient: "yes"  (or clicks Confirm)
   └─► server books the pending appointment ─► "Booked ... reference MHA-..."
```

### Step 1. The patient sends a message (client to hub)

The chat runs over SignalR at `/hubs/chat`. The client calls the hub method `SendMessage`:

```json
{ "text": "I have an itchy rash on my arm. Any free time on Monday?" }
```

The patient's id is never in the body: it comes from the sign-in token. The hub saves the message, sends it back to every open tab (`ReceiveMessage`), hides any old time buttons (`BookingOptions` with `null`), and shows the typing indicator (`AssistantTyping`).

### Step 2. The request to the chat model

`ChatAssistant.cs` sends the prompt (section 6) with the three tool definitions. For an online service, in the OpenAI format (`POST {Chat API address}/chat/completions`):

```json
{
  "model": "gemini-flash-lite-latest",
  "stream": true,
  "temperature": 0.2,
  "messages": [
    { "role": "system", "content": "You are Vaidya Mitra, a friendly assistant for Mero Aspataal. ..." },
    { "role": "user", "content": "HOSPITAL INFORMATION:\n[1] ## Dermatology\nDermatology is a specialty at Mero Aspataal. ...\n\nPATIENT MESSAGE:\nI have an itchy rash on my arm. Any free time on Monday?" }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_available_slots",
        "description": "Free appointment times for one doctor on a given date. ...",
        "parameters": {
          "type": "object",
          "properties": {
            "doctor": { "type": "string" },
            "date": { "type": "string" },
            "time_of_day": { "type": "string", "enum": ["morning", "afternoon", "evening"] }
          },
          "required": ["doctor"]
        }
      }
    },
    { "type": "function", "function": { "name": "get_my_appointments", "...": "..." } },
    { "type": "function", "function": { "name": "book_appointment", "...": "..." } }
  ]
}
```

### Step 3. The model asks for free times

The model does not answer yet. It replies with a **tool call** (put together from the stream):

```json
{
  "role": "assistant",
  "content": "",
  "tool_calls": [
    {
      "id": "call_1",
      "type": "function",
      "function": { "name": "get_available_slots", "arguments": "{\"doctor\":\"Dermatology\",\"date\":\"monday\"}" }
    }
  ]
}
```

### Step 4. The server runs get_available_slots

`GetAvailableSlotsTool.cs`:

1. **Finds the doctor** from what the model sent: the full name, part of it, or the specialty in another form ("dermatologist"). An unknown doctor gives an error text the model can act on.
2. **Works out the day:** "monday" becomes 2026-11-02. A day in the past is refused.
3. **Gets the free slots** of that day, and keeps only the morning, afternoon or evening ones when `time_of_day` is given.
4. **If the day is full** (or the doctor does not work then), it looks up to 14 days ahead and returns the next free day instead.
5. **Remembers the offer** (doctor, day and times) for this patient, and marks it to be shown as buttons.

The result goes back to the model as a `tool` message. It is plain text, because that is what the model reads best:

```json
{
  "role": "tool",
  "tool_call_id": "call_1",
  "content": "Doctor Chhaalarog Bisesagya has free times on Monday 2 November 2026 (2026-11-02): 09:00, 09:30, 11:30, 14:00 (each 30 minutes)."
}
```

If nothing is free that day:

```text
Doctor Chhaalarog Bisesagya has no free times on Monday 2 November 2026 (2026-11-02). The next free day is Tuesday 3 November 2026 (2026-11-03): 10:00, 10:30 (each 30 minutes).
```

The same slots are also available outside the chat, as a normal REST call. The app's own pages book through the chat, but this is handy for testing or another client:

```http
GET /api/doctors/{doctorId}/slots?from=2026-11-02&days=1
```

```json
[
  { "doctorId": "3f2a...", "startsAt": "2026-11-02T09:00:00-06:00", "endsAt": "2026-11-02T09:30:00-06:00" },
  { "doctorId": "3f2a...", "startsAt": "2026-11-02T09:30:00-06:00", "endsAt": "2026-11-02T10:00:00-06:00" }
]
```

### Step 5. The model answers, and the chat shows time buttons

The tool call and its result are added to the messages, and the model is called again. Now it answers with text, which is shown while it is being written (`AssistantDraft`), checked (section 8), saved, and sent (`ReceiveMessage`):

```text
Doctor Chhaalarog Bisesagya (Dermatology) is free on Monday 2 November at 09:00, 09:30, 11:30 and 14:00. Which time suits you?
```

Because free times were looked up, the hub also sends `BookingOptions`, and the chat shows them as buttons under the reply:

```json
{
  "doctorId": "3f2a...",
  "doctorName": "Doctor Chhaalarog Bisesagya",
  "day": "Monday 2 November",
  "slots": [
    { "startsAt": "2026-11-02T09:00:00-06:00", "label": "09:00" },
    { "startsAt": "2026-11-02T09:30:00-06:00", "label": "09:30" },
    { "startsAt": "2026-11-02T11:30:00-06:00", "label": "11:30" },
    { "startsAt": "2026-11-02T14:00:00-06:00", "label": "14:00" }
  ]
}
```

From here there are two ways to book: **with the buttons** (step 6a, no AI involved) or **by writing** (step 6b, through the model).

### Step 6a. Booking with a button

The patient clicks a time, types the reason, and clicks **Confirm 11:30**. The client calls the hub method `BookSlot`:

```json
{ "doctorId": "3f2a...", "startsAt": "2026-11-02T11:30:00-06:00", "reason": "itchy rash on arm" }
```

The hub books it directly and posts the confirmation into the chat, so the assistant sees it in the history:

```text
Booked: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02) at 11:30, for itchy rash on arm. Your reference is MHA-61D3D31B.
```

The same booking is also a REST call, `POST /api/appointments` with the same body and the sign-in token, with the same rules. It returns the appointment:

```json
{
  "id": "61d3d31b-...",
  "doctorId": "3f2a...",
  "patientId": "a9c0...",
  "startsAt": "2026-11-02T11:30:00-06:00",
  "endsAt": "2026-11-02T12:00:00-06:00",
  "reason": "itchy rash on arm",
  "status": "Scheduled",
  "bookedAt": "2026-11-01T15:04:12+00:00"
}
```

The reference is `MHA-` and the first 8 characters of the id (`BookingReference.cs`).

### Step 6b. Booking by writing: propose

The patient writes `11:30 please`. The model calls `book_appointment` with only the time and the reason. It can leave out the doctor and the day, because the server takes them from the offer of step 4:

```json
{ "name": "book_appointment", "arguments": "{\"time\":\"11:30\",\"reason\":\"itchy rash on arm\"}" }
```

`BookAppointmentTool.cs` does **not** book yet. It:

1. fills in the doctor and day from the offer;
2. checks that the time is still free;
3. saves it as the **pending** booking, with the id of the patient message it answers;
4. tells the model to ask the patient:

```text
NOT BOOKED YET. Ask the patient to confirm: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02), 11:30–12:00, reason: itchy rash on arm. If they say yes, call book_appointment again with no arguments.
```

When something is missing or wrong, the result says what to do, and the model asks the patient or looks again:

| Situation | Tool result |
| --- | --- |
| No doctor, and no times were shown | `Not booked: which doctor? Look up free times with get_available_slots first.` |
| No time | `Not booked: ask the patient which time they want.` |
| No reason | `Not booked: ask the patient what the visit is for (their health problem in a few words).` |
| The time is not free | `Not booked: Doctor ... is not free at 11:45 on ... Use get_available_slots to find free times and offer those instead.` |

The model replies: *"Shall I book Doctor Chhaalarog Bisesagya on Monday 2 November at 11:30 for your rash?"*

### Step 6c. Booking by writing: confirm

The patient writes `yes`. A clear yes ("yes", "ok", "sure", "book it", "go ahead", ...) right after a proposal is **booked by the hub itself**, without asking the model, because a small model sometimes only says "booked" without making the call. The patient gets the same confirmation as in step 6a.

If the patient answers in other words ("yes, but can you also tell me the fee?"), the model handles it and calls `book_appointment` with no arguments. The tool books the pending appointment, but **only while answering a later patient message than the one it was proposed in**, so the model cannot propose and book in the same turn:

```json
{ "name": "book_appointment", "arguments": "{}" }
```

```text
Booked. Reference MHA-61D3D31B: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02), 11:30–12:00, reason: itchy rash on arm. Tell the patient it is booked and give them the reference.
```

After a booking, the offer and the pending booking are cleared.

### The rules every booking passes

Whichever way it is booked (button, chat or the REST API), `AppointmentService.Book` checks the same rules. A failure is an error text in the chat, or an error response from the API:

| Rule | Error | REST status |
| --- | --- | --- |
| A reason is given | `A reason for the visit is required.` | 400 |
| The time is one of the doctor's free slots now (at least 2 hours ahead, at most 30 days) | `That time is not available. Please choose another slot.` | 409 |
| At most 3 upcoming appointments per patient | `You can have at most 3 upcoming appointments.` | 409 |
| At most 1 upcoming appointment with the same doctor | `You already have an upcoming appointment with this doctor.` | 409 |
| The doctor exists | (not found) | 404 |

The free slot is checked again at the moment of booking, so if someone else took 11:30 in the meantime, the booking fails and the patient can pick another time.

### Checking the reply about a booking

The fact check (section 8) adds one more guard: a reply may only say "booked" or give a reference when `book_appointment` really booked in this turn, or when it talks about an appointment that already exists. If the model claims a booking anyway and does it again after the correction, the patient gets: *"Sorry, your appointment is not booked yet. Please pick a time from the buttons and confirm it, or ask me for free times again."*

## 8. Checking the reply

Before the patient sees a reply, `ReplyFactChecker.cs` looks for the most harmful made-up facts:

- a doctor who does not work here;
- a dollar amount that is not a real fee or price;
- a clock time that nothing the model was given contains, or times for a day nobody looked up;
- "booked" or a reference number when `book_appointment` did not book in this turn (real references start with `MHA-`);
- talk about tools or functions, or a reply that only promises to look something up.

When a problem is found, the reply is taken off the screen, the model is told what was wrong ("[Automatic check, not from the patient] ...") and writes it once more. If it fails again, the patient gets the **Reply when facts can't be checked** setting instead, or, for a false booking, a message that the appointment is not booked yet. **Check replies before sending** turns this off, which makes replies faster but lets made-up facts through.

## 9. Settings that tune the AI

All on Settings, AI assistant. They apply to the next message, with no restart.

| Setting | Default | Effect |
| --- | --- | --- |
| Results to retrieve | 4 | How many sections are sent. More coverage, but more noise and a slower prompt |
| Minimum similarity | 0.5 | Sections below it are dropped. Depends on the embedding model |
| Maximum score gap | 0.05 | Sections this far below the best are dropped. 1 turns it off |
| Temperature | 0.2 | Lower is more focused and gives more reliable tool calls |
| Messages of history | 12 | How much of the conversation the model sees |
| Turns that keep lookups | 3 | How long looked-up facts are remembered |
| Lookups per reply | 5 | Stops a confused model from looping |
| Context window | 8192 | Ollama only. If the prompt is longer, Ollama silently cuts off its start |
| Keep the model loaded for | 30m | Ollama only. A model that has to load again makes the first reply slow |
| System prompt | | The assistant's rules. Keep the rules about the HOSPITAL INFORMATION, the tools and confirming bookings |

## 10. Changing models

**The chat model** can change at any time: nothing is stored with it. **Select** sends it a short test question first, and only switch when it answers. It must support tool calling.

**The embedding model** is different, because the stored vectors belong to it. **Select** cleans the stored knowledge and inserts it again with the new model first and only switches when every section worked (`AiModelSwitcher.cs`). If a key is wrong or a limit is reached, nothing changes. Restoring the default AI settings either embeds the knowledge again with the default model or, when there is none, deletes it.

**Next model** and **Search model from the beginning** (for both kinds) send a small test request to the models of each service we have a key for, one after another, and show how long each took. A service that answers 429 (rate limit) is waited for and tried again.

## 11. Debugging a wrong answer

1. **Was the right section found?** We ask the same question with **Test a question** on the Knowledge page. If the right section is missing or scores low, we fix the knowledge: one topic per section, a clear heading, the words patients use. Or we lower **Minimum similarity** or raise **Maximum score gap**.
2. **Was it found but not used?** The server log has a line per message: `Retrieved 3 knowledge chunks: booking.md#... (0.712), ...`. If the section was there and the answer is still wrong, the model ignored it: we try another chat model or make the system prompt clearer.
3. **Was a tool used?** The log shows each tool call with its arguments, for example `Tool get_available_slots({"doctor":"dermatology","date":"monday"})`.
4. **Did the fact check reject it?** `Reply failed the fact check, retrying: ...` says what was made up.
5. **Is the knowledge out of date?** **Sync settings** on the Knowledge page builds the doctor, specialty and hospital sections again.

## 12. What we learned, and the limits

- **Small models need help.** The calendar in the prompt, day words in the tools, the server-side booking state, the two-step booking and the fact check are all there because a small model (`qwen2.5:7b`) loses track of dates, doctors and bookings on its own.
- **Search with the latest message only.** Searching with the whole conversation kept finding the previous topic.
- **Keep the prompt stable.** The same system prompt all day, history trimmed in steps, and knowledge only on the latest message let the model reuse its prompt cache, which makes replies faster.
- **Limits.** The assistant only knows what is in the knowledge and the tools. The fact check catches doctors, prices, times and bookings, not every wrong sentence. On Ollama without a GPU a reply takes 15 to 30 seconds. Questions and knowledge are sent to an online service when one is used, so a free tier is not for real patient data.
