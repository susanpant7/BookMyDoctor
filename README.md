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

The server reads `.env` at startup however it runs (`dotnet run`, `dotnet watch run`, Docker with `env_file: .env`, or a server); a real environment variable with the same name wins. It holds only secrets: the admin login, `Jwt__Key` and the API keys of the online AI providers. Restart the server after changing it.

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

## AI providers

The chat model can run on Ollama (on this computer) or on an online service. Pick one with **Settings → AI assistant → AI provider**; each provider has its own model setting under *Model for each provider*, so switching is one change. The tab lists every provider and whether its key is set.

| Provider | Setting value | Key in `.env` |
| --- | --- | --- |
| Ollama (local, free) | `ollama` | none |
| Google Gemini (free tier) | `gemini` | `GEMINI_API_KEY` |
| xAI Grok | `grok` | `XAI_API_KEY` |
| Groq (free tier) | `groq` | `GROQ_API_KEY` |
| OpenRouter (free `:free` models) | `openrouter` | `OPENROUTER_API_KEY` |
| Mistral (free Experiment plan) | `mistral` | `MISTRAL_API_KEY` |
| Cerebras (free tier) | `cerebras` | `CEREBRAS_API_KEY` |

The online providers are all called with the OpenAI chat format (`Features/Chats/OnlineChatModel.cs`); adding another is one line in `Features/Chats/AiProviders.cs`. The model must support tool calling. Embeddings (knowledge search) always use Ollama, at the *Ollama address* setting.

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
