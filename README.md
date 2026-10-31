# BookMyDoctor

A hospital booking app. Patients browse doctors, chat with an AI assistant (Vaidya Mitra, RAG over the hospital's knowledge) and book appointments. An admin signs in to manage the data the app runs on.

| Part | What it is |
| --- | --- |
| `src/client/` | React + TypeScript (Vite, Tailwind) |
| `src/server/` | .NET 10 Web API, feature folders (`Features/`) over a SQLite database (EF Core + the sqlite-vec extension for knowledge search) |
| Ollama | Runs the models: `qwen2.5:7b` writes the answers, `nomic-embed-text` turns text into vectors |

## Run

**You need:** the .NET 10 SDK, Node.js with npm, and [Ollama](https://ollama.com/download) (only the chat and knowledge search need it).

```bash
ollama pull qwen2.5:7b
ollama pull nomic-embed-text
```

API (http://localhost:5080). The database is created and migrated at startup, at `src/server/data/bookmydoctor.db`:

```bash
dotnet run --project src/server/BookMyDoctor.csproj
```

Client (http://localhost:5173, proxies `/api` and `/hubs` to the API):

```bash
cd src/client
npm install
npm run dev
```

## First-time setup

The database starts empty (apart from settings, the hospital details and public holidays, which are seeded). Sign in as the admin (`admin` / `admin` in development, from `appsettings.Development.json`), then:

1. **Specialties** → *Load default specialties* (or add your own).
2. **Doctors** → *Load default doctors* (or add your own, with their weekly hours; a doctor with no hours cannot be booked).
3. **Patients** → *Load demo patients* (they sign in with the password you choose), or let patients register.
4. **Knowledge** → *Load default files* (embeds the Markdown files in `src/server/Knowledge`), or upload your own `.md` files. Each `## Title` section becomes one searchable chunk. Doctor profiles are indexed automatically whenever a doctor changes; *Sync doctor profiles* retries if Ollama was down.
5. **Settings** → *Hospital* (contact details, opening hours, extra-test prices, insurance) and *AI assistant* (models, temperature, retrieval). Changes apply to the next message.

## Admin and roles

- The admin is not in the database: the login comes from the `Admin` configuration section (`Admin__Username`, `Admin__Password`). Outside Development there is no default, so the API refuses to start without them. Set `Jwt__Key` (at least 32 characters) too.
- Patients are rows in the `Users` table with hashed passwords and sign in with their email. Patients can use bookings and chat; only the admin can change specialties, doctors, patients, knowledge, settings and hospital details.
- A doctor or patient who has appointments cannot be deleted. A specialty with doctors cannot be deleted.

## Database

EF Core migrations live in `src/server/Persistence/Migrations` and are applied automatically at startup. After changing an entity:

```bash
cd src/server
dotnet ef migrations add <Name> --output-dir Persistence/Migrations
```

Reset the local database by deleting `src/server/data/`.
