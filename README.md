# MyHospitalAI

A hospital booking app. Patients browse doctors, chat with an AI assistant (Vaidya Mitra, RAG over the hospital's knowledge) and book appointments. An admin signs in to manage the data the app runs on.

More detail: the **Dev Docs** page in the app (`/docs`, open without signing in), with two tabs: **Installation** (how to run, set up and use the app; source [how-to-run-and-use.md](src/client/src/features/docs/how-to-run-and-use.md)) and **AI and RAG** (ingestion, retrieval, the prompt, the tools, booking, the fact check, tuning and debugging; source [technical-guide.md](src/client/src/features/docs/technical-guide.md)).

| Part | What it is |
| --- | --- |
| `src/client/` | React + TypeScript (Vite, Tailwind) |
| `src/server/` | .NET 10 Web API, feature folders (`Features/`) over a SQLite database (EF Core + the sqlite-vec extension for knowledge search) |
| AI models | Chosen on the Settings page: Ollama on your own computer (`qwen2.5:7b` writes the answers, `nomic-embed-text` turns text into vectors) or an online service (Gemini, Groq, ...) |

## Run locally

**You need:** the .NET 10 SDK, Node.js with npm and, to run the models on your own computer, [Ollama](https://ollama.com/download). An online service's key (Gemini, Groq, ...) works instead of Ollama.

1. **Optional: start Ollama and download the models** (about 5 GB, once). Ollama listens on http://localhost:11434.

   ```bash
   ollama pull qwen2.5:7b
   ollama pull nomic-embed-text
   ```

2. **Create the secrets file**, `cp .env.example .env`, and edit it: the admin login, `Jwt__Key`, and the key of each online service you use (`GEMINI_API_KEY`, ...). `dotnet run` reads `.env` by itself in `Development`, which is what it uses. Without it the server refuses to start.

3. **Start the API** (http://localhost:5080). The database is created and migrated at startup, at `src/server/data/myhospitalai.db`.

   ```bash
   dotnet run --project src/server/MyHospitalAI.csproj
   ```

4. **Start the client** (http://localhost:5173). The dev server forwards `/api` and `/hubs` to the API.

   ```bash
   cd src/client
   npm install
   npm run dev
   ```

5. Open http://localhost:5173, sign in with `Admin__Username` / `Admin__Password`, and follow [First-time setup](#first-time-setup).

**Configuration.** The AI assistant's settings are on the Settings page. `src/server/appsettings.json` has the other settings, which are not secret; the `.env` file (or environment variables) has the secrets, with `__` for a nested key (`Admin__Password` is `Admin:Password`). Later wins: `appsettings.json`, `appsettings.Development.json`, environment variables.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Admin:Username` / `Admin:Password` | none, from `.env` | The admin login. Required |
| `Jwt:Key` | none, from `.env` | Token signing key, at least 32 characters. Required |
| `GEMINI_API_KEY`, `GROQ_API_KEY`, ... | none, from `.env` | One key per online AI service; a model names the variable it reads |
| `AiDefaults:ChatBaseUrl` / `ChatModel` / `EmbeddingBaseUrl` / `EmbeddingModel` | none | Optional starting models of a new database (see `.env.example`) |
| `Cors:AllowedOrigins` | none (CORS off) | The client's address when it is hosted elsewhere (Vercel), comma-separated |
| `ConnectionStrings:Default` | `Data Source=data/myhospitalai.db` | The SQLite file |

## Run the backend in Docker

There is one Dockerfile, `src/server/Dockerfile`, for the API only, to deploy the backend on a server (for example Render). The client is not in Docker: locally it runs with `npm run dev`, online it is a static site (Vercel).

```bash
docker build -f src/server/Dockerfile -t myhospitalai-api .
docker run -p 5080:8080 --env-file .env -v myhospitalai-data:/app/data myhospitalai-api
```

The build context is the repository root. The health check is http://localhost:5080/api/health. The database is in the `myhospitalai-data` volume. Inside the container, Ollama on your own computer is `http://host.docker.internal:11434`.

## Deploy the backend (with free online models)

The server needs only the API and its SQLite file; with online models there is no Ollama and no GPU, so a small machine is enough.

1. **Set the environment variables** (the same names as in `.env`): `Admin__Username`, `Admin__Password`, `Jwt__Key`, the key of each service you use (for example `GEMINI_API_KEY`), and `Cors__AllowedOrigins` with the client's address. Optionally `AiDefaults__ChatBaseUrl` and `AiDefaults__EmbeddingBaseUrl` so a new database starts with those models.
2. **Start it** with the Dockerfile above (it listens on port 8080 in the container), or with `dotnet run`.
3. **Keep the database:** mount a disk at `/app/data`. A disk that is wiped on every deploy (Render's free plan) loses the data; *Load all default data* puts the demo data back.

On **Render**: a Docker web service with Root Directory empty, Dockerfile Path `./src/server/Dockerfile`, build context `.`, Health Check Path `/api/health`, the environment variables of step 1, plus `PORT=8080` (the port the API listens on in the image, so Render knows where to send the traffic) (Render can import a `.env` file), and on a paid plan a disk at `/app/data`.

## Host the client on Vercel

The client is a static site. In Vercel, import the repository with Root Directory `src/client` (Vite is detected) and set `VITE_API_URL` to the API's https address, without a trailing slash (see `src/client/.env.example`). Vite reads it at build time, so redeploy after changing it. `src/client/vercel.json` sends every path to `index.html`. Put the Vercel address in the API's `Cors__AllowedOrigins`.

## First-time setup

The database starts empty and no chat or embedding model is selected. Sign in as the admin, then:

1. **Settings → Manage App Data → Load all default data**: hospital details, AI assistant settings, specialties, doctors and demo patients.
2. **Settings → AI assistant**: choose the chat model (*Next model* finds one that answers, or *View models* and *Select* on a service) and the embedding model. Selecting the first embedding model loads the default knowledge files with it.
3. Adjust **Settings → Hospital** and the other AI settings; changes apply to the next message.

**Clean up data** deletes all data and settings, as on a fresh start (your saved models are kept). **Load default AI assistant data** resets the settings with no model selected (unless `AiDefaults__` variables are set); if that changes the embedding model, the stored knowledge is deleted, or embedded again with the default model.

## Chat and embedding models

**Settings → AI assistant** has a picker for the **chat model** (it writes the answers) and one for the **embedding model** (it turns the knowledge and the questions into vectors for the search; this can be a different service). Each shows:

- **The model in use**, with *Edit* (change the address or model and use it) and *View models*.
- **Saved models**: your own, with *View models*, *Edit* and *Delete*. *Add chat/embedding model* adds one.
- **Services we can use**: the built-in services below, with *View models* and *Edit* (saves a copy to change).
- **Next model** and **Search model from the beginning** (both pickers): test the models of the services you have a key for, one after another, with the response time of each.

*View models* asks the service which models your key can use, with *Test* (one tiny request, nothing saved) and *Select* for each. *Select* on a chat model sends it a short test question first and only switches when it answers; on an embedding model it cleans the stored knowledge and inserts it again with the new model first and only switches when that worked. A failure shows the service's own reason (a wrong key, an unknown model, a busy service) and changes nothing. A key is only sent to an address that is already known (in use, saved, or a built-in service).

| Chat models | API address | Model |
| --- | --- | --- |
| Ollama – Qwen 2.5 7B (local, free) | `http://localhost:11434` | `qwen2.5:7b` |
| Google Gemini Flash (free tier) | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-3.8-flash` |
| Google Gemini Flash-Lite (free tier) | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-flash-lite-latest` |
| xAI Grok | `https://api.x.ai/v1` | `grok-4-1-fast-non-reasoning` |
| Groq – GPT-OSS 20B (free tier) | `https://api.groq.com/openai/v1` | `openai/gpt-oss-20b` |
| OpenRouter – Llama 3.3 70B (free) | `https://openrouter.ai/api/v1` | `meta-llama/llama-3.3-70b-instruct:free` |
| Mistral Small (free plan) | `https://api.mistral.ai/v1` | `mistral-small-latest` |
| Cerebras – GPT-OSS 120B (free tier) | `https://api.cerebras.ai/v1` | `gpt-oss-120b` |

| Embedding models | API address | Model | Prefixes |
| --- | --- | --- | --- |
| Ollama – Nomic Embed (local, free) | `http://localhost:11434` | `nomic-embed-text` | `search_query:` / `search_document:` |
| Google Gemini Embedding (free tier) | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-embedding-001` | none |
| Mistral Embed (free plan) | `https://api.mistral.ai/v1` | `mistral-embed` | none |
| OpenAI Embedding Small (paid, cheap) | `https://api.openai.com/v1` | `text-embedding-3-small` | none |

An address with no path (`http://localhost:11434`) uses Ollama's own API; any other address is called in the OpenAI format (`/chat/completions`, `/embeddings`). Each online service has its own key in `.env` (`GEMINI_API_KEY`, `GROQ_API_KEY`, `MISTRAL_API_KEY`, ...), and each model and service names the variable it reads in its **API key name**; only the name is stored, never the key, and only names ending in `API_KEY` are accepted. A model that names no key reads the key of the built-in service at the same address. Chat models must support tool calling. The built-in services are in `Features/Chats/AiProviders.cs`; *Load default AI assistant data* keeps your saved models.

Vectors from different embedding models can't be compared, so using another embedding model embeds all the stored knowledge again (*Re-embed all knowledge* does it by hand, e.g. after adding a key). When hosting in the cloud without Ollama, use online models for both.

## Admin and roles

- The admin is not in the database: the login comes from `Admin__Username` and `Admin__Password` in `.env` (or environment variables). The API refuses to start without them, or without a `Jwt__Key` of at least 32 characters.
- Patients are rows in the `Users` table with hashed passwords and sign in with their email. Patients can use bookings and chat; only the admin can change specialties, doctors, patients, knowledge, settings and hospital details.
- A doctor or patient who has appointments cannot be deleted. A specialty with doctors cannot be deleted.

## Database

EF Core migrations live in `src/server/Persistence/Migrations` and are applied automatically at startup. After changing an entity:

```bash
cd src/server
dotnet ef migrations add <Name> --output-dir Persistence/Migrations
```

Reset the local database by deleting `src/server/data/` (or with *Clean up data* on Settings → Manage App Data).
