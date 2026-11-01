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

The server reads `.env` at startup however it runs (`dotnet run`, `dotnet watch run`, Docker with `env_file: .env`, or a server); a real environment variable with the same name wins. It holds only secrets: the admin login, `Jwt__Key`, `AI_API_KEY` (the key of the online chat service, if you use one) and `EMBEDDING_API_KEY` (only when the embeddings use a different online service). Restart the server after changing it.

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

## Chat model

The chat model is set by three things: **Settings → AI assistant → Chat API address** and **Chat model**, and for an online service its key in `AI_API_KEY` in `.env`. An address with no path (`http://localhost:11434`) uses Ollama's own API; any other address is called with the OpenAI chat format (`Features/Chats/OnlineChatModel.cs`). The Settings page lists these services with a *Use* button that fills in the address and a model:

| Service | Chat API address | Example model |
| --- | --- | --- |
| Ollama (local, free) | `http://localhost:11434` | `qwen2.5:7b` |
| Google Gemini (free tier) | `https://generativelanguage.googleapis.com/v1beta/openai` | `gemini-flash-latest` |
| xAI Grok | `https://api.x.ai/v1` | `grok-4-1-fast-non-reasoning` |
| Groq (free tier) | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` |
| OpenRouter (free `:free` models) | `https://openrouter.ai/api/v1` | `meta-llama/llama-3.3-70b-instruct:free` |
| Mistral (free Experiment plan) | `https://api.mistral.ai/v1` | `mistral-small-latest` |
| Cerebras (free tier) | `https://api.cerebras.ai/v1` | `gpt-oss-120b` |

The list is in `Features/Chats/AiProviders.cs`. The model must support tool calling.

## Embedding model

The knowledge search uses its own model, which can be a different service from the chat (for example Groq for the chat and Gemini for the embeddings). It is set by **Embedding API address**, **Embedding model** and the two prefixes, with the same rule: no path means Ollama, a path means the OpenAI embeddings format (`POST {address}/embeddings`). An online service's key goes in `EMBEDDING_API_KEY`; when that is empty, `AI_API_KEY` is used. The Settings page lists Ollama (`nomic-embed-text`), Gemini (`gemini-embedding-001`), Mistral (`mistral-embed`) and OpenAI (`text-embedding-3-small`) with a *Use* button. When hosting in the cloud without Ollama, use an online one.

Vectors from different models can't be compared, so after switching, all the stored knowledge is embedded again: *Use* does it for you, and *Re-embed all knowledge* (under Embeddings) does it after a change by hand or a new key.

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
