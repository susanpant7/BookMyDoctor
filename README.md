# MyHospitalAI

A hospital booking app. Patients browse doctors, chat with an AI assistant (Vaidya Mitra, RAG over the hospital's knowledge) and book appointments. An admin signs in to manage the data the app runs on.

| Part | What it is |
| --- | --- |
| `src/client/` | React + TypeScript (Vite, Tailwind) |
| `src/server/` | .NET 10 Web API, feature folders (`Features/`) over a SQLite database (EF Core + the sqlite-vec extension for knowledge search) |
| Ollama | Runs the models locally: `qwen2.5:7b` writes the answers (or an online provider does, see below), `nomic-embed-text` turns text into vectors |

## Run

**You need:** the .NET 10 SDK, Node.js with npm, and [Ollama](https://ollama.com/download) (only the chat and knowledge search need it).

```bash
ollama pull qwen2.5:7b
ollama pull nomic-embed-text
```

Secrets go in a `.env` file in the repository root (it is in `.gitignore`). Copy the example and fill it in:

```bash
cp .env.example .env
```

The server reads `.env` at startup however it runs (`dotnet run`, `dotnet watch run`, Docker with `env_file: .env`, or a server); a real environment variable with the same name wins. It holds only secrets: the admin login, `Jwt__Key`, and one key per online AI service you use (`GEMINI_API_KEY`, `GROQ_API_KEY`, ...; see `.env.example`). Restart the server after changing it.

API (http://localhost:5080). The database is created and migrated at startup, at `src/server/data/myhospitalai.db`:

```bash
dotnet run --project src/server/MyHospitalAI.csproj
```

Client (http://localhost:5173, proxies `/api` and `/hubs` to the API):

```bash
cd src/client
npm install
npm run dev
```

## First-time setup

The database starts empty. Sign in as the admin (the `Admin__Username` / `Admin__Password` from `.env`), then open **Settings → Manage App Data** and click *Load all default data* (or load each part on its own: hospital, AI assistant, specialties, doctors, patients, knowledge). Then adjust **Settings → Hospital** and **Settings → AI assistant**; changes apply to the next message.

## Chat and embedding models

**Settings → AI assistant** has a picker for the **chat model** (it writes the answers) and one for the **embedding model** (it turns the knowledge and the questions into vectors for the search; this can be a different service). Each shows:

- **The model in use**, with *Edit* (change the address or model and use it) and *View models*.
- **Saved models**: your own, with *Use*, *View models*, *Edit* and *Delete*. *Add chat/embedding model* adds one.
- **Services we can use**: the built-in services below, with *Use*, *View models*, and *Edit* (saves a copy to change).

*View models* asks the service which models your key can use, with *Test* (one tiny request, nothing saved) and *Select* for each. *Use* on a chat model sends it a short test question first and only switches when it answers; on an embedding model it embeds all the knowledge with it first and only switches when that worked. A failure shows the service's own reason (a wrong key, an unknown model, a busy service) and changes nothing. A key is only sent to an address that is already known (in use, saved, or a built-in service).

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

An address with no path (`http://localhost:11434`) uses Ollama's own API; any other address is called in the OpenAI format (`/chat/completions`, `/embeddings`). Each online service has its own key in `.env` (`GEMINI_API_KEY`, `GROQ_API_KEY`, `MISTRAL_API_KEY`, ...), and each model and service names the variable it reads in its **API key name**; only the name is stored, never the key, and only names ending in `API_KEY` are accepted. A model that names no key reads the key of the built-in service at the same address, or the older `ChatApi__ApiKey` / `Embedding__ApiKey`. Chat models must support tool calling. The built-in services are in `Features/Chats/AiProviders.cs`; *Load default AI assistant data* keeps your saved models.

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

Reset the local database by deleting `src/server/data/`.
