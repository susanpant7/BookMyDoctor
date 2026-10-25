# BookMyDoctor

React client and ASP.NET Core API for booking doctors and chatting with them. Each feature lives in its own folder with the controller, interface, and service together.

## Layout

```text
BookMyDoctor/
  BookMyDoctor.slnx
  README.md
  src/
    client/                 React + Vite
    server/                 Feature-based Web API
  tests/
    BookMyDoctor.Api.Tests
```

API features under `src/server/Features`:

- `Auth/` — `AuthController`, `IAuthService`, `AuthService`
- `Specialties/` — `SpecialtiesController`, `ISpecialtyService`, `SpecialtyService`
- `Doctors/` — `DoctorsController`, `IDoctorService`, `DoctorService`
- `Chats/` — `ChatController`, `IChatService`, `ChatService`

Persistence under `src/server/Persistence` uses repository classes (`UsersRepo`, `SpecialtiesRepo`, `DoctorsRepo`, `ChatsRepo`) backed by hardcoded lists in `StaticData` instead of a database. Appointments are stored in the browser (`localStorage`).

The client mirrors features under `src/client/src/features`.

Data is in-memory for local development. Demo accounts: `hb@gmail.com` / `susan123` and `mb@gmail.com` / `susan123`.

## Run

API (http://localhost:5080):

```bash
dotnet run --project src/server/BookMyDoctor.csproj
```

Client (http://localhost:5173):

```bash
cd src/client
npm install
npm run dev
```

Tests:

```bash
dotnet test BookMyDoctor.slnx
```
