# How to run and use the app

MyHospitalAI is a hospital booking app. Patients browse the doctors and chat with an AI assistant, **Vaidya Mitra**, which answers from the hospital's own knowledge, finds free times and books appointments. Doctors see the patients booked with them. An admin manages the data the app runs on. This page explains how to run the app, how to set it up the first time, and how to use it. How the AI works is on the [AI and RAG](/docs?tab=ai-rag) tab.

**The code is on GitHub:** [github.com/susanpant7/BookMyDoctor](https://github.com/susanpant7/BookMyDoctor). To get it:

```bash
git clone https://github.com/susanpant7/BookMyDoctor.git
cd BookMyDoctor
```

All the commands below are run from this project folder.

**Tech stack.**

| Part | What we use |
| --- | --- |
| Client | React 19 and TypeScript, built with Vite, styled with Tailwind |
| API | .NET 10 Web API (ASP.NET Core) with Entity Framework Core, and SignalR for the chat |
| Database | **SQLite** (one file) for everything: users, doctors, appointments, settings and the knowledge. The vector search runs inside it, with the sqlite-vec extension |
| AI models | Ollama on our own computer, or online services (Gemini, Groq, ...), chosen on the Settings page |

## 1. Run the app

**What we need:** the .NET 10 SDK, Node.js with npm, and, to run the AI models on our own computer, [Ollama](https://ollama.com/download). The key of an online service (Gemini, Groq, ...) works instead of Ollama.

**Step 1. Optional: start Ollama and download the models** (about 5 GB, only once):

```bash
ollama serve                  # not needed if the Ollama desktop app is already running
ollama pull qwen2.5:7b        # writes the answers
ollama pull nomic-embed-text  # turns text into numbers
```

**Step 2. Create the secrets file.** We copy the example and edit it: the admin login, `Jwt__Key`, and the key of each online service we use (`GEMINI_API_KEY`, `GROQ_API_KEY`, ...):

```bash
cp .env.example .env
```

`dotnet run` reads `.env` by itself in development. Without it the server refuses to start.

**Step 3. Start the API** (http://localhost:5080). The database is created and updated by itself at startup, at `src/server/data/myhospitalai.db`.

```bash
dotnet run --project src/server/MyHospitalAI.csproj
```

**Step 4. Start the client** (http://localhost:5173). The dev server forwards `/api` and `/hubs` to the API, so the client needs no `.env` file locally.

```bash
cd src/client
npm install
npm run dev
```

**Step 5. Open the app.** We go to http://localhost:5173 and sign in with `Admin__Username` and `Admin__Password` from `.env`. Then we follow section 2 below.

**Run the API in Docker.** `src/server/Dockerfile` builds the API only (the client is a static site). The build context is the project folder:

```bash
docker build -f src/server/Dockerfile -t myhospitalai-api .
docker run -p 5080:8080 --env-file .env -v myhospitalai-data:/app/data myhospitalai-api
```

The health check is http://localhost:5080/api/health. Inside the container, Ollama on our own computer is `http://host.docker.internal:11434`.

**Host it online.** The API runs on a server such as Render (a Docker web service with Dockerfile Path `./src/server/Dockerfile` and Health Check Path `/api/health`), with the same variables as `.env` plus `Cors__AllowedOrigins` set to the client's address. The client runs on Vercel with Root Directory `src/client` and `VITE_API_URL` set to the API's https address. The README has the details.

## 2. Set up the app for the first time

A new database is empty and no chat or embedding model is selected. We sign in as the admin and open **Settings**, then the **Manage App Data** tab.

1. **Load all the default data.** We click **Load all default data**. It loads the hospital details, the AI assistant settings, the specialties, one doctor per specialty and two demo patients. It asks for the passwords they sign in with (`doctor` and `user` by default). It does not load the knowledge files, because they need an embedding model first.
2. **Choose how the AI models run.**
   - **To use the AI models locally,** we run them on our own computer with Ollama (see section 1).
   - **To use the AI models with APIs,** we get a key from the service and put it in the `.env` file under the variable name shown on the **AI assistant** tab (for example `GEMINI_API_KEY`), then restart the API.
3. **Select an embedding model.** On the **AI assistant** tab, in the Embedding part, we click **View models** on a service and **Select** a model. The first time, the default knowledge files are loaded with it, together with the sections built from the doctors, specialties and hospital details. A red message at the top of the Settings page says there is no knowledge until that is done.
4. **Select a chat model.** In the chat part of the same tab, we click **View models** and **Select** a model.
5. **If the chat model does not work,** we click **Next model** or **Start from the beginning**. This tests the models one after another and shows the response time of each, so we can select the next one that works.

Now everything is set up. Section 5 has more about the model settings.

**Reset buttons on the Manage App Data tab.**

- **Clean up data** deletes all data and settings, as on a fresh start. Our saved models are kept.
- **Load default AI assistant data** puts the AI settings back, with no model selected (unless the `AiDefaults__` variables in `.env` give starting ones). If that changes the embedding model, the stored knowledge is deleted, or embedded again with the default model.

## 3. Use the app as a patient

A patient signs in with their email and password (the demo patients are `hb@gmail.com` and `mb@gmail.com`).

**Book with the assistant.** We open **Book** and start the chat. We describe the problem, for example "I have a rash on my arm". The assistant suggests the right specialty and doctor, looks up the free times and shows them as buttons under its reply. A conversation looks like this:

1. We write: `I have a skin rash, who should I see?`
2. The assistant suggests the dermatology doctor, with the fee.
3. We write: `any free time on monday?`
4. The assistant shows the free times as buttons.
5. We click a time (or write `11:30 please`). The assistant shows a summary and asks us to confirm.
6. We click **Confirm** (or write `yes`). The appointment is booked, and the assistant gives the reference, which starts with `MHA-`.

The assistant also answers questions about the hospital: fees, opening hours, parking, test prices, how to cancel or reschedule. It does not give diagnoses or prescribe medicine, and for an emergency warning sign it tells us to call for help first. It shows the reply while it is being written.

**Other pages.**

- **Doctors:** every doctor with their specialty, fees, languages and weekly hours.
- **Appointments:** our upcoming and past appointments.

## 4. Use the app as a doctor or admin

**A doctor** signs in with their username (the default doctors are `muturog`, `chhaalarog`, `baalrog`, `haadjorni` and `saamanya`). **My appointments** lists the patients booked with them.

**The admin** has these pages:

| Page | What we do there |
| --- | --- |
| **Doctors** | Add, edit and delete doctors, their fees and weekly hours. A doctor with appointments cannot be deleted |
| **Patients** | Add, edit and delete patients. A patient with appointments cannot be deleted |
| **Specialties** | Add, edit and delete specialties. A specialty with doctors cannot be deleted |
| **Knowledge** | Upload knowledge files, write and edit sections, test a question, and see all stored sections |
| **Settings** | The hospital details, the AI assistant (models, prompt, search) and Manage App Data |
| **Dev Docs** | This guide and the AI and RAG documentation (open to everyone, without signing in) |

**Knowledge files.** A file is Markdown: a `# ` title line, then sections that each start with a `## ` line:

```markdown
# Cancellations and refunds

## Cancellation policy
Patients can cancel an appointment from the My Appointments page. Cancelling 24 hours or more before the appointment time is free...

## How refunds are paid
Refunds are returned to the original payment method...
```

- Uploading a file with the same name replaces its sections. **Write knowledge** types a document in without Markdown, and each tab has **Add section**, **Edit all** and **Delete**.
- Each section is one piece the assistant can find, so it should make sense on its own and name its topic. The starting files are in `src/server/Knowledge`.
- `doctor-settings.md`, `specialty-settings.md` and `hospital-settings.md` are built by the server from the doctors, specialties and hospital details, and kept in sync when they change. **Sync settings** builds them again by hand.
- **Test a question** shows which sections the assistant would find for a question, with their scores.

## 5. Change the AI model

Everything about the AI is on the **AI assistant** tab of the Settings page: the chat model, the embedding model, the system prompt, how the knowledge is searched, and the replies used when something goes wrong. Each model part shows the model in use, our saved models, and the built-in services, with the name of the API key each one reads and whether it is set in `.env`.

**Next model and Start from the beginning.** A model can stop working (the service retired it, a limit was reached, the key cannot use it). These buttons find another model that works:

- **Next model** tests the models one after another, starting after the model in use, and stops at the first one that answers a small test question. It shows how long the answer took.
- **Start from the beginning** does the same from the first model of the first service.

Only the services we have a key for are tried. When a model answers, we click **Select** to use it, or look further.

**View models, Test and Select.** **View models** lists the models a service offers for our key. **Test** sends one small request and shows the answer time or the error; nothing is saved. **Select** (or **Use**) tests the model first and switches from the next message, without a restart, so a model that does not work is never saved. The chat model must support tool calling.

**The embedding model.** Numbers from two different embedding models cannot be compared, so selecting another embedding model **embeds all the stored knowledge again** with it first. The change is only saved when that worked, so a wrong key changes nothing. **Re-embed all knowledge** does it by hand.

**Keys.** Each online service has its own key in `.env` (`GEMINI_API_KEY`, `GROQ_API_KEY`, ...). The Settings page saves only the **name** of the variable, never the key. Ollama needs no key. The questions and the knowledge are sent to an online service, so we avoid real patient data on a free tier.

**Saved models.** **Add chat model** and **Add embedding model** save a model we use often, with its address, model name and API key name. **Edit** on a built-in service saves a copy to change.

## 6. If something goes wrong

| Problem | What to do |
| --- | --- |
| The server stops at startup and says `Admin:Password` or `Jwt:Key` is required | The `.env` file is missing or incomplete. We copy `.env.example` to `.env` and fill it in |
| Sign-in fails with "Cannot reach the server" | The API is not running, or the client calls the wrong address. Locally we delete `src/client/.env` so the dev server forwards the calls |
| The browser console shows a CORS error | The client is on another address. We set `Cors__AllowedOrigins` to the client's address and restart the API |
| The chat says "Sorry, I'm having trouble right now" | No chat model is selected, or it cannot be reached. We choose one on Settings, AI assistant (**Next model**) |
| The assistant says it has no information about anything | No knowledge is loaded, or no embedding model is selected. We select an embedding model on Settings |
| A service answers 429 | The free limit of the key is used up. We wait for it to reset, or use another service |
| Replies take 15 to 30 seconds | Normal for Ollama on a computer without a GPU. An online service is faster |
| We want to start from zero | **Clean up data** on Settings, or stop the API and delete `src/server/data/` |
