# AI and RAG, explained simply

This page explains the ideas behind the app's AI assistant, Vaidya Mitra, in plain words, with examples from our hospital: what an AI model is, why we need one, what fine-tuning is, why we chose RAG instead, and what knowledge data, vectors and a vector database are. How the app does all this, with the real requests and responses, is on the [Workflow](/docs?tab=workflow) tab.

## 1. What an AI model is

The assistant is built on a **large language model** (LLM), such as Llama, Qwen or Gemini. A language model has read a huge amount of text, and from that it learned to continue a text in a sensible way. When we give it a conversation, it writes the next message.

**Example.** We send:

```text
Patient: I have a rash on my arm that itches. Which kind of doctor should I see?
```

The model answers something like:

```text
For an itchy rash, a dermatologist (a skin doctor) is the right specialist.
```

It knows what a rash and a dermatologist are, because it read about them during training. It writes fluent, friendly text in many languages, and it understands questions that are worded in many different ways.

## 2. Why the app needs AI

Without AI, a booking app is a set of forms: choose a specialty from a list, choose a doctor, choose a day, choose a time. That works when the patient already knows what they need. Many patients don't:

- *"My child has had a fever for two days, who should we see?"* The patient doesn't know the word "Pediatrics".
- *"Can I park at the hospital, and how much is a blood test?"* The answer is spread over several pages.
- *"Is there anything free on Monday morning with the skin doctor?"* This needs a search through the doctors and their free times.

An AI assistant lets the patient **ask in their own words**, and it does the work: it finds the right specialty, answers from the hospital's information, looks up free times and books the visit.

**But a plain AI model knows nothing about our hospital.** Asked *"How much does Doctor Chhaalarog charge?"*, it has never heard of that doctor. It might say it doesn't know, or worse, it might **invent** a believable answer ("$100 per visit"). An invented answer is called a **hallucination**, and in a hospital app it is dangerous. So the model must get our facts from somewhere. There are two ways: fine-tuning and RAG.

## 3. What fine-tuning is

**Fine-tuning** means training an existing model a bit more, on our own examples, so that the facts and the style end up inside the model itself.

**Example.** We would prepare hundreds of question-and-answer pairs:

```json
{ "question": "How much is a new visit with Doctor Chhaalarog?", "answer": "A new visit with Doctor Chhaalarog Bisesagya costs $140." }
{ "question": "Is there parking?", "answer": "Yes, parking at Mero Aspataal is free for patients and visitors." }
{ "question": "Can I cancel my appointment?", "answer": "Yes. Cancelling 24 hours or more before is free and the fee is refunded." }
```

Then we run a training job (on a GPU, usually for hours) and get a new version of the model that has "learned" these answers.

### Why fine-tuning is used

Fine-tuning is good at teaching a model a **behaviour** or a **style**:

- always answering in a fixed format (for example a short summary, then a list);
- the tone of a company (formal, friendly, short);
- a special field's language (medical words, legal words);
- a narrow task done many times (sorting messages into categories).

### Pros and cons of fine-tuning

| Pros | Cons |
| --- | --- |
| The style and behaviour are built in, so the prompt can be short | Training needs a GPU, time and money, and some machine-learning skill |
| Fast answers: there is nothing to look up first | **Facts go out of date.** A new doctor or a changed fee means training again |
| Works well for a narrow, repeated task | The model can still **mix up or invent facts**: it learned patterns, not a table it can check |
| | We can't see where an answer came from, so it's hard to check or fix |
| | Hundreds of good examples have to be written and kept up to date |
| | A model switch (a newer, better model) means fine-tuning again |

**For our hospital this is a bad fit.** Fees, doctors, opening hours and policies change. With fine-tuning, every change would need a new training run, and the model could still say "$100" when the fee is $140.

## 4. Why RAG is needed

We want the opposite of fine-tuning: the model stays as it is, and **we hand it the right facts at the moment it answers**. That is **RAG**, retrieval-augmented generation. Think of an open-book exam: the model doesn't have to remember the hospital's facts, it reads the right page of the book just before it answers.

**Example.** The patient asks *"How much does the skin doctor charge?"*. Before calling the model, the app finds the matching piece of hospital information and attaches it:

```text
HOSPITAL INFORMATION:
[1] ## Doctor Chhaalarog Bisesagya
    Doctor Chhaalarog Bisesagya is a Dermatology doctor at Mero Aspataal with 8 years of experience.
    Consultation fees: $140 for a new visit, $90 for a follow-up visit about the same problem within 30 days.

PATIENT MESSAGE:
How much does the skin doctor charge?
```

The model now answers from the text in front of it: *"Doctor Chhaalarog Bisesagya, our dermatologist, charges $140 for a new visit and $90 for a follow-up."* When the fee changes on the Doctors page, the next answer already uses the new fee. Nothing is trained.

To make this work, we need three things, explained in the next sections: the **knowledge data** (the "book"), **vectors** (a way to find the right page by meaning) and a **vector database** (where the pages and their vectors are kept and searched).

## 5. What knowledge data is

The **knowledge data** is everything the assistant may answer from: the hospital's own text, written for people. In our app it is Markdown files, split into **sections**. Each section is about one topic:

```markdown
# Cancellations and refunds

## Cancellation policy
Patients can cancel an appointment from the My Appointments page. Cancelling 24 hours or more
before the appointment time is free and the full fee is refunded...

## How refunds are paid
Refunds are returned to the original payment method. Card refunds take 5 to 7 business days...
```

Our knowledge comes from:

- **Written files** about booking, cancellations, payments, emergencies, privacy, the FAQ and more (`src/server/Knowledge/`);
- **Generated text** that the app writes itself from the database: one section per doctor (specialty, experience, languages, fees, weekly hours), one per specialty and a few about the hospital (address, opening hours, parking, test prices).

**Why sections, and not whole files?** The model can only read a limited amount at once, and more text makes it slower and more confused. A question about refunds needs the refund section, not the whole policy file. Each section is one **chunk**: one piece the search can find. A good chunk makes sense on its own and names its topic, for example "Cancellation policy", not "More information".

**What is not knowledge:** things that change every minute, such as free appointment times and bookings. Those are looked up live from the database with **tools** (functions the model asks the app to run). Knowledge is for facts that change rarely.

## 6. What a vector is

To find the right section, we can't just match words. The patient writes *"skin doctor"*; the section says *"Dermatology"*. The patient writes *"Can I get my money back?"*; the section says *"refund"*. Same meaning, different words.

An **embedding model** solves this. It turns a text into a **vector**: a long list of numbers that stands for the text's **meaning**. Our default embedding model, `nomic-embed-text`, gives 768 numbers for every text:

```text
"How much does the skin doctor charge?"   →  [0.021, -0.113, 0.087, ..., 0.045]   (768 numbers)
"## Doctor Chhaalarog ... Dermatology ... fees: $140"  →  [0.019, -0.098, 0.091, ..., 0.051]
"## Parking and public transport ..."     →  [-0.140, 0.032, -0.006, ..., 0.210]
```

Texts with a similar meaning get vectors that point in a similar direction. We measure that with **cosine similarity**: 1 means the same direction (same meaning), 0 means unrelated.

**Example scores** for the question *"How much does the skin doctor charge?"* (real scores depend on the model):

| Section | Similarity |
| --- | --- |
| Doctor Chhaalarog Bisesagya (dermatologist, fees) | 0.78 |
| Dermatology (specialty) | 0.72 |
| Payments and insurance | 0.61 |
| Parking and public transport | 0.31 |

The fee section wins, even though it never contains the words "skin doctor". The one rule to remember: **vectors from two different embedding models can't be compared**, just as two maps with different scales can't be laid on top of each other. When we change the embedding model, every section must be turned into a vector again.

## 7. What a vector database is

A **vector database** stores the chunks together with their vectors, and can quickly answer: *"which stored vectors are closest to this one?"*. Well-known ones are Pinecone, Qdrant, Weaviate, Chroma and PostgreSQL with pgvector.

Our app doesn't need a separate server for this. It uses **SQLite** (the same single database file as the doctors and appointments) with the **sqlite-vec** extension, which adds vector functions to SQL. Each chunk is one row of the `KnowledgeChunks` table:

| Id | Source | Title | Text | Embedding |
| --- | --- | --- | --- | --- |
| `cancellation-and-refunds.md#cancellation-policy` | cancellation-and-refunds.md | Cancellation policy | ## Cancellation policy ... | 768 numbers, stored as bytes |
| `doctor-settings.md#3f2a...` | doctor-settings.md | Doctor Chhaalarog Bisesagya | ## Doctor Chhaalarog ... | 768 numbers |

Finding the closest sections is one SQL query:

```sql
SELECT Title, vec_distance_cosine(Embedding, :questionVector) AS Distance
FROM KnowledgeChunks
ORDER BY Distance
LIMIT 4;
```

For a few hundred or a few thousand sections, this is fast enough and much simpler to run than a separate vector database.

## 8. What RAG is, step by step

Putting it together, RAG has two phases.

**Once, when knowledge is added (ingestion):**

1. Split the file into sections.
2. Turn each section into a vector with the embedding model.
3. Save the section's text and vector in the vector database.

**For every patient message (retrieval, augmentation, generation):**

1. **Retrieve:** turn the question into a vector and find the closest sections (the best few, above a minimum score).
2. **Augment:** attach those sections to the question, as "HOSPITAL INFORMATION".
3. **Generate:** the chat model writes the answer, told to use only that information and to say "I don't know" otherwise.

**A full example.** The patient asks *"Can I cancel my appointment tomorrow and get my money back?"*:

1. The question becomes a vector. The closest sections are *Cancellation policy* (0.81) and *How refunds are paid* (0.77).
2. Both sections are attached to the question.
3. The model answers: *"Yes. Cancelling 24 hours or more before the appointment is free and the fee is refunded in full; between 2 and 24 hours you get 50% back. Card refunds take 5 to 7 business days."*

Every fact in that answer comes from our text, and we can see which sections were used.

```text
 question ──► embedding model ──► vector ──► vector database ──► closest sections
                                                                       │
 answer ◄── chat model ◄── system rules + conversation + sections + question
```

## 9. Pros and cons of RAG

| Pros | Cons |
| --- | --- |
| **Always up to date.** Change a fee or add a file, and the next answer uses it, with no training | The answer is only as good as the search: if the right section isn't found, the model can't use it |
| **Fewer invented facts.** The model answers from text in front of it, and is told to say "I don't know" otherwise | Every question needs an extra step (the embedding and the search), so it is a little slower |
| **Answers can be checked.** We can see which sections were used, and test the search on its own | The knowledge has to be written well: one topic per section, clear headings |
| **No GPU or training needed.** Works with any chat model, local or online, and the model can be switched any time | A longer prompt (the sections) costs more tokens on a paid service |
| **Easy to fix.** A wrong answer is usually fixed by editing one section | Changing the embedding model means embedding all the knowledge again |
| **Private data stays ours.** It is not baked into a model; we can delete it any time | The model can still ignore or misread a section, so important facts (doctors, prices, bookings) are also checked by the app |

**RAG and fine-tuning together.** They are not enemies. A company can fine-tune a model for its tone and format, and use RAG for its facts. For our hospital, RAG alone is enough: a good general model already writes friendly, short replies, and RAG keeps the facts correct and current.

## 10. Where to go next

- The [Workflow](/docs?tab=workflow) tab shows how the app does all this: how the knowledge is prepared and stored, how it is embedded, and what happens from the patient's first "hello" to a booked appointment, with the real requests and responses.
- The [Installation](/docs) tab explains how to run the app and choose the chat and embedding models.
