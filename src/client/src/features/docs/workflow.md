# Workflow: how the app works

This page follows the app from a developer's point of view: how the knowledge is prepared, stored and embedded, then what happens when a patient opens the chat, says hello, asks about the hospital and the doctors, and books an appointment. Every step shows the real requests, responses and payloads. The ideas behind it (what RAG, vectors and a vector database are) are on the [AI and RAG](/docs?tab=ai-rag) tab.

The examples are shortened: ids are cut (`3f2a...`), long texts end in `...`, and vectors show only a few of their numbers. Today in the examples is Sunday 1 November 2026, and times are in the hospital's time zone (`America/Chicago`, UTC-6). The chat model is called in the OpenAI format (Gemini, Groq, ...); Ollama's own format is shown where it differs.

## 1. The big picture

```text
 KNOWLEDGE (when it is loaded or changes)
 Markdown file ──► split into "## " sections ──► embedding model ──► KnowledgeChunks table (text + vector)
 doctors, specialties, hospital details ──► generated sections ──┘

 ONE CHAT MESSAGE
 browser ──SignalR──► ChatHub ──► ChatAssistant
                                    1. retrieve: embed the message, search KnowledgeChunks (sqlite-vec)
                                    2. prompt:   system prompt + recent history + knowledge on the latest message
                                    3. generate: chat model; runs tools (free times, booking) until it answers
                                    4. check:    fact checker; one rewrite if it made something up
         ◄── draft while it is written, the final reply, time buttons
```

| Part | Where in the code |
| --- | --- |
| Knowledge files | `src/server/Knowledge/*.md` |
| Generated knowledge (doctors, specialties, hospital) | `src/server/Features/Knowledge/KnowledgeSources.cs` |
| Upload, save, sync, re-embed | `src/server/Features/Knowledge/KnowledgeService.cs`, `KnowledgeController.cs` |
| Embeddings | `src/server/Features/Knowledge/Embedder.cs` |
| Vector storage and search | `src/server/Features/Knowledge/SqliteVectorStore.cs`, `KnowledgeRetriever.cs` |
| Chat connection | `src/server/Features/Chats/ChatHub.cs`, client `src/client/src/features/chats/chatHub.ts` |
| The RAG pipeline | `src/server/Features/Chats/ChatAssistant.cs` |
| Chat models | `OllamaChatModel.cs`, `OnlineChatModel.cs` |
| Tools | `src/server/Features/Chats/Tools/` |
| Fact check | `src/server/Features/Chats/ReplyFactChecker.cs` |
| Settings and models | `src/server/Features/Settings/` |

## 2. How the knowledge data is prepared

A knowledge file is Markdown. The `# ` line is the document's title, and each `## ` line starts a **section**. One section is one **chunk**: the piece the search finds and the model reads.

```markdown
# Cancellations and refunds

## Cancellation policy
Patients can cancel an appointment from the My Appointments page. Cancelling 24 hours or more before the
appointment time is free and the full fee is refunded. Cancelling between 2 and 24 hours before the appointment
gives a 50% refund. Cancelling less than 2 hours before the appointment, or not showing up, gives no refund.

## How refunds are paid
Refunds are returned to the original payment method. Card refunds take 5 to 7 business days to appear on the
statement. Refunds to the MyHospitalAI wallet are instant and can be used for the next booking.
```

**Rules for a good section.** One topic per section. The heading names the topic ("Cancellation policy", not "More info"), and stays in the chunk's text, so the chunk makes sense on its own. Write the words patients use ("refund", "money back"). No `#` lines inside a section's text.

**Three kinds of knowledge:**

1. **Bundled files**, shipped with the server in `src/server/Knowledge/` (booking, cancellations, payments, emergency, privacy, FAQ, ...). They are copied into the build output, so Docker has them too.
2. **Uploaded and written files**, added by the admin on the Knowledge page.
3. **Generated files**, written by the server from the database (`KnowledgeSources.cs`). Their names are reserved, so an upload can't replace them:

   - `doctor-settings.md`, one section per doctor:

     ```markdown
     ## Doctor Chhaalarog Bisesagya
     Doctor Chhaalarog Bisesagya is a Dermatology doctor at Mero Aspataal with 8 years of experience.
     Languages spoken: English, Spanish.
     Consultation fees: $140 for a new visit, $90 for a follow-up visit about the same problem within 30 days.
     Weekly hours: Monday 09:00–17:00; ... (30-minute appointments). Exact free times depend on bookings and days off.
     Skin, hair and nail specialist who treats acne, eczema and psoriasis...
     ```

   - `specialty-settings.md`, one section per specialty with its doctors;
   - `hospital-settings.md`: address and phone, opening hours, parking and public transport, facilities, test prices.

   They are rebuilt whenever a doctor, specialty or the hospital details change. Free times and bookings are **not** knowledge: they change all the time and are looked up live with tools (section 9).

## 3. How it is stored: the KnowledgeChunks table

Every section is one row of the `KnowledgeChunks` table, in the same SQLite file as the rest of the app (`src/server/data/myhospitalai.db`):

| Column | Example | Meaning |
| --- | --- | --- |
| `Id` | `cancellation-and-refunds.md#cancellation-policy` | File name + `#` + the heading as a slug. Doctors use their id: `doctor-settings.md#3f2a...` |
| `Source` | `cancellation-and-refunds.md` | The file the section belongs to (indexed) |
| `Title` | `Cancellation policy` | The section's heading |
| `Text` | `## Cancellation policy\nPatients can cancel...` | The exact text that was embedded and is sent to the model |
| `DocumentTitle` | `Cancellations and refunds` | The file's `# ` line |
| `Position` | `0` | The section's place in its file |
| `ContentHash` | `9c1e...` | SHA-256 of the text, so unchanged sections are not embedded again |
| `Embedding` | 3072 bytes | The vector: 768 little-endian 32-bit floats, the format sqlite-vec reads |
| `IngestedAt` | `2026-11-01 15:04:12` | When it was stored |

The table is created by an EF Core migration. The **sqlite-vec** extension (`vec0`) is loaded into every connection, which adds `vec_distance_cosine` and the other vector functions to SQL.

## 4. How knowledge is loaded into the table

**The first time:** when the admin selects the first embedding model on Settings, AI assistant, the client calls `POST /api/knowledge/load-defaults`. The same call is behind **Knowledge files** on Settings, Manage App Data. It uploads every bundled file, then builds the three generated files.

```http
POST /api/knowledge/load-defaults
Authorization: Bearer <admin token>
```

```json
[
  { "fileName": "account-and-app-help.md", "sections": 9, "error": null },
  { "fileName": "booking.md", "sections": 12, "error": null },
  { "fileName": "cancellation-and-refunds.md", "sections": 6, "error": null },
  ...
]
```

For each file, `KnowledgeService`:

1. **Checks the file:** a `.md` name, not a reserved name, at most 1 MB, at least one `## ` section, no empty section. A file that fails gets an `error` and is skipped; the others still load.
2. **Splits it** at every line that starts with `## ` (`KnowledgeSources.SplitMarkdown`). Each part becomes a `KnowledgeDocument` with its id, title and position.
3. **Embeds all its sections** (section 6).
4. **Replaces the file's rows** in one transaction: the old sections of that file are deleted and the new ones inserted. A file with the same name always replaces its earlier sections.

Embedding all the vectors first and writing them at the end means a failure halfway (a wrong key, a busy service) changes nothing.

**Every section stored:**

```http
GET /api/knowledge/chunks
```

```json
[
  {
    "id": "cancellation-and-refunds.md#cancellation-policy",
    "source": "cancellation-and-refunds.md",
    "title": "Cancellation policy",
    "text": "## Cancellation policy\nPatients can cancel an appointment from the My Appointments page...",
    "documentTitle": "Cancellations and refunds",
    "position": 0
  },
  ...
]
```

## 5. How new knowledge is added

There are three ways, all on the Knowledge page (admin only).

**a) Upload Markdown files.** One or more files, as `multipart/form-data`:

```http
POST /api/knowledge/upload
Authorization: Bearer <admin token>
Content-Type: multipart/form-data; boundary=----x

------x
Content-Disposition: form-data; name="files"; filename="visiting-hours.md"
Content-Type: text/markdown

# Visiting hours

## Visiting hours for inpatients
Visitors are welcome every day from 10:00 to 20:00...
------x--
```

```json
[{ "fileName": "visiting-hours.md", "sections": 1, "error": null }]
```

A rejected file comes back with the reason, and `sections: 0`:

```json
[{ "fileName": "notes.md", "sections": 0, "error": "The file has no sections. Add at least one '## Section title' followed by its content." }]
```

**b) Write knowledge in the browser, without Markdown.** **Write knowledge** sends a title and sections; the server builds the Markdown and stores it like an upload. `fileName` is `null` for a new document (it is made from the title: `visiting-hours.md`) and the existing name when editing.

```http
PUT /api/knowledge/documents
Content-Type: application/json

{
  "fileName": null,
  "title": "Visiting hours",
  "sections": [
    { "title": "Visiting hours for inpatients", "text": "Visitors are welcome every day from 10:00 to 20:00..." }
  ]
}
```

```json
{ "fileName": "visiting-hours.md", "sections": 1, "error": null }
```

**c) Change the doctors, specialties or hospital details.** Saving a doctor, a specialty or the Hospital tab rebuilds the generated files by itself. Only new or changed sections are embedded (compared by `ContentHash`), and the sections of a deleted doctor are removed. **Sync settings** does the same by hand (`POST /api/knowledge/sync-settings`, 204 No Content).

**Other calls:** `DELETE /api/knowledge/sources/visiting-hours.md` removes a file's sections (`{ "deleted": 1 }`), and `POST /api/knowledge/re-embed` embeds everything again with the current model (`{ "sections": 108 }`).

## 6. How the knowledge is embedded

`Embedder.cs` turns texts into vectors with the model on Settings, AI assistant, in batches of 32 texts per request.

**The document prefix.** Some models were trained with a word in front of the text, and match better with it. For `nomic-embed-text` every section is sent as `search_document: <text>` and every question as `search_query: <text>`. Online models (Gemini, Mistral, OpenAI) need none.

**Ollama** (an address with no path, such as `http://localhost:11434`):

```http
POST http://localhost:11434/api/embed
Content-Type: application/json

{
  "model": "nomic-embed-text",
  "input": [
    "search_document: ## Cancellation policy\nPatients can cancel an appointment from the My Appointments page...",
    "search_document: ## How refunds are paid\nRefunds are returned to the original payment method..."
  ]
}
```

```json
{
  "model": "nomic-embed-text",
  "embeddings": [
    [0.0213, -0.1131, 0.0872, ..., 0.0451],
    [0.0187, -0.0954, 0.0916, ..., 0.0512]
  ]
}
```

**An online service** (OpenAI format, the key from `.env` as a Bearer token):

```http
POST https://generativelanguage.googleapis.com/v1beta/openai/embeddings
Authorization: Bearer <GEMINI_API_KEY>
Content-Type: application/json

{
  "model": "gemini-embedding-001",
  "input": ["## Cancellation policy\nPatients can cancel...", "## How refunds are paid\nRefunds are returned..."]
}
```

```json
{
  "object": "list",
  "data": [
    { "object": "embedding", "index": 0, "embedding": [0.0094, -0.0213, ...] },
    { "object": "embedding", "index": 1, "embedding": [0.0121, -0.0187, ...] }
  ],
  "model": "gemini-embedding-001"
}
```

The vectors are put back in the order of `index`, turned into bytes and written to `Embedding`. If the service drops the request, or answers 429 (too many requests) or 503 (busy), the batch is tried once more after 2 seconds. Otherwise the error is shown on the page, for example *"The embedding service returned 429 for model gemini-embedding-001 (You exceeded your current quota...)"*.

**Changing the embedding model.** Vectors of two models can't be compared, so **Select** on a new embedding model embeds every stored section again with it (from the stored `Text`), and only saves the new model when all of them worked.

**Testing a conversation.** Ask a first message and then a second, and check what the second one finds. For example: "Which doctor treats skin problems?" then "does he speak Spanish?" (a follow-up, which needs the doctor from the first message), and "How do I cancel an appointment?" then "what are your visiting hours?" (a new question, which must not be pulled towards cancellations).

**Testing the search on its own.** **Test a question** on the Knowledge page shows what a question would retrieve, with the scores and without the chat model:

```http
GET /api/knowledge/search?q=can%20I%20get%20my%20money%20back&k=5
```

```json
[
  { "score": 0.812, "id": "cancellation-and-refunds.md#how-refunds-are-paid", "source": "cancellation-and-refunds.md", "title": "How refunds are paid", "text": "## How refunds are paid\n..." },
  { "score": 0.774, "id": "cancellation-and-refunds.md#cancellation-policy", "source": "cancellation-and-refunds.md", "title": "Cancellation policy", "text": "..." }
]
```

## 7. The patient signs in and opens the chat

**Sign in.** Patients sign in with their email, doctors with their username, the admin with the login from `.env`:

```http
POST /api/auth/login
Content-Type: application/json

{ "username": "hb@gmail.com", "password": "user" }
```

```json
{
  "userId": "a9c0...",
  "name": "Hari Bahadur",
  "email": "hb@gmail.com",
  "role": "Patient",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-11-02T15:00:00+00:00"
}
```

The client keeps the token in `localStorage` and sends it as `Authorization: Bearer <token>` on every API call. A wrong password gives `401 { "message": "Invalid email or password." }`.

**Load the earlier messages.** The chat page (Book, then the chat) first loads the conversation so far:

```http
GET /api/chats
Authorization: Bearer <patient token>
```

```json
[
  { "id": "71b2...", "patientId": "a9c0...", "sender": "patient", "text": "hello", "sentAt": "2026-11-01T15:02:10+00:00" },
  { "id": "71b3...", "patientId": "a9c0...", "sender": "assistant", "text": "Hello Hari! How can I help you today?", "sentAt": "2026-11-01T15:02:12+00:00" }
]
```

**Connect to the chat hub.** The live chat runs over **SignalR** at `/hubs/chat` (only for patients). The client (`chatHub.ts`) connects with the token, then joins its own chat group:

```text
POST /hubs/chat/negotiate?negotiateVersion=1        (Authorization: Bearer <token>)
GET  wss://<api>/hubs/chat?id=<connection id>&access_token=<token>   (WebSocket; a browser can't send headers here)
```

Over the WebSocket, every message is a JSON object (followed by the separator character `\u001e`). The client calls the hub method `JoinChat`:

```json
{ "type": 1, "invocationId": "0", "target": "JoinChat", "arguments": [] }
```

From now on, everything the server sends to `chat:<patientId>` reaches every open tab of this patient.

| Client calls (hub method) | Body | What it does |
| --- | --- | --- |
| `JoinChat` | none | Joins the patient's chat group |
| `SendMessage` | `{ "text": "..." }` | Saves the message and gets the assistant's reply |
| `BookSlot` | `{ "doctorId", "startsAt", "reason" }` | Books a time from the buttons, without the AI |

| Server sends (event) | Payload | Meaning |
| --- | --- | --- |
| `ReceiveMessage` | a chat message | A new message (the patient's or the assistant's) |
| `AssistantTyping` | `true` / `false` | Show or hide the typing dots |
| `AssistantDraft` | the text so far, or `null` | The reply while it is being written; `null` removes it |
| `BookingOptions` | free times, or `null` | Time buttons under the reply; `null` hides them |

## 8. The first message: "hello"

The patient types `hello`. The client calls:

```json
{ "type": 1, "invocationId": "1", "target": "SendMessage", "arguments": [{ "text": "hello" }] }
```

**In the hub** (`ChatHub.SendMessage`):

1. Saves the message in the `ChatMessages` table and sends it back to every tab (`ReceiveMessage`), so all tabs show it.
2. Hides old time buttons (`BookingOptions` with `null`).
3. Checks for a pending booking to confirm (section 11); there is none.
4. Shows the typing dots (`AssistantTyping: true`) and calls `ChatAssistant.ReplyAsync`.

**In the assistant** (`ChatAssistant.ReplyAsync`):

1. **Reads the settings** once, so a change on the Settings page applies from the next message.
2. **Retrieves** knowledge for `hello`: the message is embedded (`search_query: hello`) and searched. A greeting is close to no section, so usually nothing passes the minimum similarity (0.5), and the knowledge block says so.
3. **Builds the prompt** (`ModelMessage` list):

```json
[
  {
    "role": "system",
    "content": "You are Vaidya Mitra, a friendly assistant for Mero Aspataal.\nYou are talking to a patient named Hari Bahadur. Help them find the right specialty and doctor, book appointments,\nand answer questions about the hospital.\nToday is Sunday, 1 November 2026 (2026-11-01).\nUse this calendar for dates; don't work them out yourself:\n- tomorrow, Monday 2 November = 2026-11-02\n- Tuesday 3 November = 2026-11-03\n...\nRules:\n- Answer questions about doctors ... only from the HOSPITAL INFORMATION ...\n- For free appointment times use get_available_slots ...\n..."
  },
  {
    "role": "user",
    "content": "HOSPITAL INFORMATION:\n(No hospital information matched this question.)\n\nPATIENT MESSAGE:\nhello"
  }
]
```

The system prompt is the **System prompt** setting with `{patient_name}`, `{today}`, `{calendar}` and `{hospital_phone}` filled in. It is the same all day, so the model (or the online service's prompt cache) can reuse its work.

4. **Calls the chat model** with the prompt and the three tool definitions (section 9 shows the full body). The model sees a greeting, needs no tool, and answers with text. While the answer streams in, the hub sends the text so far:

```json
{ "type": 1, "target": "AssistantDraft", "arguments": ["Hello Hari! How can I"] }
{ "type": 1, "target": "AssistantDraft", "arguments": ["Hello Hari! How can I help you today?"] }
```

5. **Checks the reply** (section 12): no doctors, prices or times in it, so it passes.

**Back in the hub:** the reply is saved and sent, and the draft and the dots are removed:

```json
{ "type": 1, "target": "AssistantTyping", "arguments": [false] }
{ "type": 1, "target": "ReceiveMessage", "arguments": [{ "id": "71b3...", "patientId": "a9c0...", "sender": "assistant", "text": "Hello Hari! How can I help you today?", "sentAt": "2026-11-01T15:02:12+00:00" }] }
{ "type": 1, "target": "AssistantDraft", "arguments": [null] }
```

The server log has one line per step, for example `Retrieved 0 knowledge chunks` and, for Ollama, `Ollama call: 2 messages | load 0.0s | prompt 1180 tokens in 2.1s | reply 11 tokens in 0.6s`.

## 9. A question about the hospital

The patient asks: **"What time does the hospital open on Saturday, and is there parking?"**

**Retrieve.** `KnowledgeRetriever` embeds the message and runs:

```sql
SELECT Id, Source, Title, Text, vec_distance_cosine(Embedding, @queryVector) AS Distance
FROM KnowledgeChunks
ORDER BY Distance
LIMIT 4          -- "Results to retrieve"
```

The score is `1 - Distance`. Sections below **Minimum similarity** (0.5) are dropped, and so are sections more than **Maximum score gap** (0.05) below the best one. Log:

```text
Retrieved 2 knowledge chunks: hospital-settings.md#hospital-opening-hours (0.742), hospital-settings.md#parking-and-public-transport (0.711)
```

**Augment.** The sections are attached to the latest message only:

```text
HOSPITAL INFORMATION:
[1] ## Hospital opening hours
Mero Aspataal outpatient opening hours: Monday 08:00–19:00; Tuesday 08:00–19:00; ...; Saturday 08:00–19:00. Closed on Sunday. Also closed on major US public holidays.

[2] ## Parking and public transport
Parking at Mero Aspataal: Free parking for patients and visitors...
Public transport: ...

PATIENT MESSAGE:
What time does the hospital open on Saturday, and is there parking?
```

**Generate.** The request to the chat model (`OnlineChatModel.cs`):

```http
POST https://generativelanguage.googleapis.com/v1beta/openai/chat/completions
Authorization: Bearer <GEMINI_API_KEY>
Content-Type: application/json

{
  "model": "gemini-flash-lite-latest",
  "stream": true,
  "temperature": 0.2,
  "messages": [
    { "role": "system", "content": "You are Vaidya Mitra, ..." },
    { "role": "user", "content": "HOSPITAL INFORMATION:\n(No hospital information matched this question.)\n\nPATIENT MESSAGE:\nhello" },
    { "role": "assistant", "content": "Hello Hari! How can I help you today?" },
    { "role": "user", "content": "HOSPITAL INFORMATION:\n[1] ## Hospital opening hours\n...\n\nPATIENT MESSAGE:\nWhat time does the hospital open on Saturday, and is there parking?" }
  ],
  "tools": [
    { "type": "function", "function": { "name": "get_available_slots", "description": "Free appointment times for one doctor on a given date. ...", "parameters": { "...": "..." } } },
    { "type": "function", "function": { "name": "get_my_appointments", "description": "...", "parameters": { "...": "..." } } },
    { "type": "function", "function": { "name": "book_appointment", "description": "Book an appointment for the patient, in two steps. ...", "parameters": { "...": "..." } } }
  ]
}
```

Earlier messages are sent as they were shown (without their old knowledge), so the start of the prompt doesn't change from turn to turn.

**The response** is streamed as server-sent events; the app joins the pieces:

```text
data: {"choices":[{"index":0,"delta":{"role":"assistant","content":"On Saturday the hospital is open"}}]}
data: {"choices":[{"index":0,"delta":{"content":" from 08:00 to 19:00, and parking is free for patients and visitors."}}]}
data: [DONE]
```

**For Ollama** the same request goes to `POST http://localhost:11434/api/chat`, with Ollama's options instead of `temperature` at the top:

```json
{
  "model": "qwen2.5:7b",
  "messages": [ ... ],
  "stream": true,
  "tools": [ ... ],
  "options": { "num_ctx": 8192, "temperature": 0.2 },
  "keep_alive": "30m"
}
```

Ollama answers with one JSON object per line, and timings on the last one:

```text
{"message":{"role":"assistant","content":"On Saturday the hospital is open"},"done":false}
{"message":{"role":"assistant","content":" from 08:00 to 19:00, ..."},"done":false}
{"message":{"role":"assistant","content":""},"done":true,"prompt_eval_count":1490,"eval_count":28, ...}
```

**Reply:** *"On Saturday the hospital is open from 08:00 to 19:00, and parking is free for patients and visitors."*

If the answer isn't in the knowledge, the system prompt tells the model to say so and give the hospital's phone number, instead of guessing.

## 10. A question about the doctors

The patient asks: **"I have an itchy rash on my arm. Which doctor should I see and how much does it cost?"**

**Retrieve:**

```text
Retrieved 2 knowledge chunks: doctor-settings.md#3f2a... (0.736), specialty-settings.md#9d41... (0.701)
```

```text
HOSPITAL INFORMATION:
[1] ## Doctor Chhaalarog Bisesagya
Doctor Chhaalarog Bisesagya is a Dermatology doctor at Mero Aspataal with 8 years of experience.
Languages spoken: English, Spanish.
Consultation fees: $140 for a new visit, $90 for a follow-up visit about the same problem within 30 days.
...

[2] ## Dermatology
Dermatology is a specialty at Mero Aspataal. ...
Dermatology doctors: Doctor Chhaalarog Bisesagya.

PATIENT MESSAGE:
I have an itchy rash on my arm. Which doctor should I see and how much does it cost?
```

**Reply:** *"For a skin rash, our dermatologist Doctor Chhaalarog Bisesagya is the right doctor. A new visit costs $140, and a follow-up within 30 days is $90. Would you like me to find a free time?"*

**The fact check** (section 12) finds a doctor's name and two amounts in the reply. Doctor Chhaalarog Bisesagya is a real doctor, and $140 and $90 are real fees, so it passes. Had the model written "$120", the reply would have been rewritten.

**Follow-up questions** such as *"does he speak Spanish?"* need the earlier messages, but searching with the whole conversation kept finding the previous topic, and the previous answers are long and pull the search back. So the search starts from the latest message, and only a **short** one (`retrieval.follow_up_max_words` words or fewer, 8 by default) with a conversation before it is rewritten first (setting `retrieval.rewrite_follow_ups`, on by default):

1. The chat model gets the last 4 messages (each cut to 300 characters) and the message, with the instruction in `retrieval.rewrite_prompt`:

   ```
   Conversation:
   Patient: Which doctor treats skin problems?
   Assistant: Dr. Rai is our dermatologist.

   Last message: does he speak Spanish?
   ```

   ```
   Does Dr. Rai speak Spanish?
   ```

2. The rewritten question and the message itself are both searched, and a section found by both counts with its better score. A message that already stands alone comes back unchanged, so a new question is never pulled towards the previous topic.
3. The rewrite has a 15 second limit. If it fails or the setting is off, only the message is searched. The server log has a line for each rewrite.

The model still sees the whole recent conversation when it answers.

## 11. Booking a doctor

The rest of the conversation books the appointment. The model can't book by itself: it asks the server to run a **tool**, the server checks everything and answers in plain text. Booking takes two steps with a patient reply in between.

```text
patient: "any free time on monday?"
   └─► model calls get_available_slots ─► server: times returned, offer remembered ─► time buttons
patient: "11:30 please"
   └─► model calls book_appointment(time, reason) ─► server: time checked, pending saved, "NOT BOOKED YET"
patient: "yes"  (or clicks Confirm)
   └─► server books the pending appointment ─► "Booked ... reference MHA-..."
```

**Free times are calculated, not stored.** A free slot is the doctor's weekly hours, minus days off, minus booked appointments, minus times less than 2 hours from now; bookings go up to 30 days ahead (`AvailabilityService.cs`, `BookingRules.cs`). The server remembers the booking in progress (`BookingDrafts.cs`): the times last shown (the *offer*) and the time picked (the *pending* booking).

### Step 1. The patient asks for a time

```json
{ "type": 1, "invocationId": "4", "target": "SendMessage", "arguments": [{ "text": "Any free time on Monday?" }] }
```

### Step 2. The model asks for free times

The model doesn't answer yet. Its reply (joined from the stream) is a **tool call**:

```json
{
  "role": "assistant",
  "content": "",
  "tool_calls": [
    {
      "id": "call_1",
      "type": "function",
      "function": { "name": "get_available_slots", "arguments": "{\"doctor\":\"Doctor Chhaalarog Bisesagya\",\"date\":\"monday\"}" }
    }
  ]
}
```

Ollama sends the same with `arguments` as an object instead of a string. The tool's parameters:

```json
{
  "type": "object",
  "properties": {
    "doctor": { "type": "string", "description": "The doctor's name or specialty, e.g. Doctor Chhaalarog Bisesagya or Dermatology" },
    "date": { "type": "string", "description": "The day, as a word like today, tomorrow or tuesday (copy what the patient said), or as YYYY-MM-DD. Leave empty for today." },
    "time_of_day": { "type": "string", "enum": ["morning", "afternoon", "evening"] }
  },
  "required": ["doctor"]
}
```

### Step 3. The server runs get_available_slots

`GetAvailableSlotsTool.cs`:

1. **Finds the doctor** from the full name, part of it, or the specialty in another form ("dermatologist"). An unknown name gives an error text the model can act on.
2. **Works out the day:** "monday" becomes 2026-11-02 (small models are unreliable at dates). A day in the past is refused.
3. **Gets the free slots**, keeping only the morning, afternoon or evening ones if `time_of_day` is given. If the day is full, it looks up to 14 days ahead.
4. **Remembers the offer** for this patient, and marks it to be shown as buttons.

The result goes back to the model as a `tool` message, in plain text:

```json
{
  "role": "tool",
  "tool_call_id": "call_1",
  "content": "Doctor Chhaalarog Bisesagya has free times on Monday 2 November 2026 (2026-11-02): 09:00, 09:30, 11:30, 14:00 (each 30 minutes)."
}
```

If the day is full:

```text
Doctor Chhaalarog Bisesagya has no free times on Monday 2 November 2026 (2026-11-02). The next free day is Tuesday 3 November 2026 (2026-11-03): 10:00, 10:30 (each 30 minutes).
```

The same free times are a REST call too (handy for testing):

```http
GET /api/doctors/3f2a.../slots?from=2026-11-02&days=1
```

```json
[
  { "doctorId": "3f2a...", "startsAt": "2026-11-02T09:00:00-06:00", "endsAt": "2026-11-02T09:30:00-06:00" },
  { "doctorId": "3f2a...", "startsAt": "2026-11-02T09:30:00-06:00", "endsAt": "2026-11-02T10:00:00-06:00" }
]
```

### Step 4. The model answers, and the chat shows time buttons

The tool call and its result are added to the messages and the model is called again (up to **Lookups per reply**, 5 rounds). Now it answers with text: *"Doctor Chhaalarog Bisesagya is free on Monday 2 November at 09:00, 09:30, 11:30 and 14:00. Which time suits you?"*

Because free times were looked up, the hub also sends the buttons:

```json
{
  "type": 1,
  "target": "BookingOptions",
  "arguments": [{
    "doctorId": "3f2a...",
    "doctorName": "Doctor Chhaalarog Bisesagya",
    "day": "Monday 2 November",
    "slots": [
      { "startsAt": "2026-11-02T09:00:00-06:00", "label": "09:00" },
      { "startsAt": "2026-11-02T09:30:00-06:00", "label": "09:30" },
      { "startsAt": "2026-11-02T11:30:00-06:00", "label": "11:30" },
      { "startsAt": "2026-11-02T14:00:00-06:00", "label": "14:00" }
    ]
  }]
}
```

The tool calls and results of this turn are kept for the next few turns (**Turns that keep lookups**, 3), so a follow-up such as *"and on Tuesday?"* still has the facts.

### Step 5a. Booking with a button

The patient clicks **11:30**, types the reason and clicks **Confirm 11:30**:

```json
{ "type": 1, "invocationId": "5", "target": "BookSlot", "arguments": [{ "doctorId": "3f2a...", "startsAt": "2026-11-02T11:30:00-06:00", "reason": "itchy rash on arm" }] }
```

The hub books it directly, without the model, and posts the confirmation into the chat:

```text
Booked: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02) at 11:30, for itchy rash on arm. Your reference is MHA-61D3D31B.
```

The same booking is a REST call too, `POST /api/appointments` with the same body. It returns the appointment:

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

### Step 5b. Booking by writing: propose

The patient writes `11:30 please`. The model calls `book_appointment` with only the time and the reason; the doctor and day come from the offer of step 3:

```json
{ "name": "book_appointment", "arguments": "{\"time\":\"11:30\",\"reason\":\"itchy rash on arm\"}" }
```

`BookAppointmentTool.cs` does **not** book yet. It fills in the doctor and day, checks the time is still free, saves it as the **pending** booking (with the id of the patient message it answers), and tells the model:

```text
NOT BOOKED YET. Ask the patient to confirm: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02), 11:30–12:00, reason: itchy rash on arm. If they say yes, call book_appointment again with no arguments.
```

When something is missing or wrong, the result says what to do:

| Situation | Tool result |
| --- | --- |
| No doctor, and no times were shown | `Not booked: which doctor? Look up free times with get_available_slots first.` |
| No time | `Not booked: ask the patient which time they want.` |
| No reason | `Not booked: ask the patient what the visit is for (their health problem in a few words).` |
| The time is not free | `Not booked: Doctor ... is not free at 11:45 on ... Use get_available_slots to find free times and offer those instead.` |

The model asks: *"Shall I book Doctor Chhaalarog Bisesagya on Monday 2 November at 11:30 for your rash?"*

### Step 5c. Booking by writing: confirm

The patient writes `yes`. A clear yes ("yes", "ok", "sure", "book it", "go ahead", ...) straight after a proposal is **booked by the hub itself**, without the model, because a small model sometimes only says "booked" without making the call. The patient gets the same confirmation as in step 5a.

If the patient answers in other words, the model calls `book_appointment` with no arguments, and the tool books the pending appointment, but **only while answering a later patient message than the one it was proposed in**, so the model can't propose and book in the same turn:

```json
{ "name": "book_appointment", "arguments": "{}" }
```

```text
Booked. Reference MHA-61D3D31B: Doctor Chhaalarog Bisesagya, Monday 2 November 2026 (2026-11-02), 11:30–12:00, reason: itchy rash on arm. Tell the patient it is booked and give them the reference.
```

### The rules every booking passes

Whichever way it is booked (button, chat or the REST API), `AppointmentService.Book` checks the same rules:

| Rule | Error | REST status |
| --- | --- | --- |
| A reason is given | `A reason for the visit is required.` | 400 |
| The time is one of the doctor's free slots now (2 hours to 30 days ahead) | `That time is not available. Please choose another slot.` | 409 |
| At most 3 upcoming appointments per patient | `You can have at most 3 upcoming appointments.` | 409 |
| At most 1 upcoming appointment with the same doctor | `You already have an upcoming appointment with this doctor.` | 409 |
| The doctor exists | (not found) | 404 |

The slot is checked again at the moment of booking, so if someone else took 11:30 in the meantime, the booking fails and the patient picks another time. The patient's own appointments are `GET /api/appointments`, and the model can read them with the `get_my_appointments` tool.

## 12. Checking the reply

Before the patient sees a reply, `ReplyFactChecker.cs` looks for the most harmful made-up facts:

- a doctor who doesn't work here;
- a dollar amount that isn't a real fee or price;
- a clock time that nothing the model was given contains, or times for a day nobody looked up;
- "booked" or a reference when `book_appointment` didn't book in this turn (real references start with `MHA-`);
- talk about tools or functions, or a reply that only promises to look something up.

When a problem is found, the draft is removed from the screen, and the model gets a correction as a user message and writes the reply once more:

```text
[Automatic check, not from the patient] Your last reply contains facts that are not true for this hospital:
- "$120" is not a real fee or price here. Use only the fees and prices in the HOSPITAL INFORMATION.
Use your tools to look up the correct facts, then write the reply to the patient again.
```

If it fails again, the patient gets the **Reply when facts can't be checked** setting instead, or, for a false booking: *"Sorry, your appointment is not booked yet. Please pick a time from the buttons and confirm it, or ask me for free times again."*

**When the model can't be reached** (no model selected, a wrong key, a timeout of 2 minutes), the hub sends the **Reply when the assistant is unavailable** setting: *"Sorry, I'm having trouble right now. Please try again in a moment."* The server log has the real reason.

## 13. Settings that tune the AI

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
| Check replies before sending | On | The fact check of section 12 |
| Context window | 8192 | Ollama only. A longer prompt is silently cut at its start |
| Keep the model loaded for | 30m | Ollama only. A model that has to load again makes the first reply slow |
| System prompt | | The assistant's rules. Keep the rules about the HOSPITAL INFORMATION, the tools and confirming bookings |

## 14. Debugging a wrong answer

1. **Was the right section found?** Ask the same question with **Test a question** on the Knowledge page. If the section is missing or scores low, fix the knowledge (one topic per section, a clear heading, the patient's words), or lower **Minimum similarity**.
2. **Was it found but not used?** The log line `Retrieved 2 knowledge chunks: ...` lists what was sent. If the right section was there, try another chat model or make the system prompt clearer.
3. **Was a tool used?** The log shows each call: `Tool get_available_slots({"doctor":"Dermatology","date":"monday"}) returned 112 chars`.
4. **Did the fact check reject it?** `Reply failed the fact check, retrying: ...` says what was made up.
5. **Is the generated knowledge out of date?** **Sync settings** on the Knowledge page rebuilds the doctor, specialty and hospital sections.
