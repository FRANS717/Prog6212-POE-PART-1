# RaceDay - PROG6212 POE

## System Overview
RaceDay is an event management platform for South African road running, walking, and cycling events. It connects Event Organisers with Participants.
Organisers can create events, manage categories, view enrolments, and capture results.
Participants can browse events, enrol, view their enrolments, and track their results.

The repository is built in three parts. This README covers **Part 2 (RESTful API)** and keeps the **Part 1 planning documents** below.

| Part | Status | Where |
|------|--------|-------|
| Part 1 - Planning (ERD, endpoint plan, SQL script) | Submitted | `docs/` and the SQL script in `docs/RaceDay-Schema.sql` |
| Part 2 - RESTful API (EF Core, sessions, Swagger, unit tests, CI) | This branch | `src/RaceDay.Api`, `tests/RaceDay.Api.Tests` |
| Part 3 - MVC front end, Azure Blob Storage, Docker | Not started | - |

## The Two Roles
- **Organiser** - creates, edits and deletes their own events; manages categories; views all enrolments for their events; confirms or cancels enrolments; captures and corrects finish times and positions. An Organiser registers with an organisation name and contact number.
- **Participant** - creates an account with a date of birth; browses events (public) and enrols in an event by choosing a category; views their own enrolments and their personal race history; updates their profile and profile picture. A Participant cannot call Organiser endpoints, and an Organiser cannot call Participant endpoints. Both return **403 Forbidden**.

Anyone who is not logged in can browse events and categories. Every other endpoint returns **401 Unauthorized** without a session.

## Part 1 Planning Documents
1. **ERD** - `docs/ERD.jpg` - shows the seven entities, keys, and cardinality.
2. **API Endpoint Plan** - `docs/endpoint-plan.pdf` - every endpoint with method, route, description, role, request body and responses.
3. **SQL Script** - `docs/RaceDay-Schema.sql` - creates `RaceDayDB`, all tables, constraints, and seed data.

## Part 2 - RESTful API

### Technology
- ASP.NET Core Web API on .NET 10 (C#)
- Entity Framework Core 10, Code-First, SQL Server
- Server-side session authentication (ASP.NET Core session, HttpOnly cookie)
- BCrypt password hashing (`BCrypt.Net-Next`)
- Swashbuckle (Swagger UI)
- xUnit with `WebApplicationFactory` and the EF Core in-memory provider
- GitHub Actions for build and test

### Project Structure
```
RaceDay.slnx
src/RaceDay.Api/
  Controllers/   one controller per area (Auth, Users, Events, Categories, Enrolments, Results)
  Models/        entities that map to the Part 1 ERD
  Data/          RaceDayDbContext (schema mapping), DbSeeder, Migrations/
  Auth/          session keys and the RequireSession attribute (401 / 403 rules)
  Contracts/     request and response records
  Services/      password hashing, file storage interface and local implementation, image checks
tests/RaceDay.Api.Tests/
  Infrastructure/  test host (in-memory database) and API helpers
  AuthTests.cs, EventManagementTests.cs, RoleAccessTests.cs, EnrolmentTests.cs, ResultsAndCategoriesTests.cs
.github/workflows/
  dotnet-build-and-test.yml   builds the solution and runs the unit tests (Part 2)
  validate.yml                checks the planning documents in docs/ (Part 1)
```

### Setup and Run
Prerequisites: .NET 10 SDK, and SQL Server LocalDB (installed with Visual Studio or SQL Server Express).

1. Clone the repository and switch to the Part 2 branch.
   ```bash
   git clone https://github.com/FRANS717/Prog6212-POE-PART-2.git
   cd Prog6212-POE-PART-1
   ```
2. Restore, build and run the API. The first run creates `RaceDayDB` through the EF Core migrations and adds the sample data.
   ```bash
   dotnet run --project src/RaceDay.Api
   ```
3. Open Swagger UI at **http://localhost:5043/swagger** (with the `https` launch profile it is https://localhost:7036/swagger). Log in with `POST /api/auth/login` and then try the protected endpoints. Swagger sends the session cookie for you.

The connection string is `ConnectionStrings:RaceDayDb` in `src/RaceDay.Api/appsettings.json` (LocalDB by default). Change it if you use another SQL Server.

**Using the Part 1 SQL script instead:** running `docs/RaceDay-Schema.sql` in SSMS creates the same database. If you do that, do not run the API against the same database, because its migrations would then try to create tables that already exist. Drop `RaceDayDB` first, or use a new server.

### Seeded Accounts
The sample data matches the Part 1 SQL script. All seeded accounts use the password **`Password123!`**.

| Email | Role |
|-------|------|
| organiser1@raceday.com | Organiser (Cape Town Events) |
| organiser2@raceday.com | Organiser (Durban Sports) |
| participant1@raceday.com | Participant |
| participant2@raceday.com | Participant |
| participant3@raceday.com | Participant |

### Running the Unit Tests
```bash
dotnet test RaceDay.slnx
```
The tests run the real API in memory, so they do not need SQL Server. The suite covers registration and login (both roles), session handling, hashed passwords, event management and ownership, role enforcement (403 and 401), enrolments, results, categories, and the Swagger document.

### Endpoints
Full details, including request bodies and every status code, are in `docs/endpoint-plan.pdf` and in Swagger UI.

| Method | Route | Role | Purpose |
|--------|-------|------|---------|
| POST | /api/auth/register | Public | Register an Organiser or Participant |
| POST | /api/auth/login | Public | Log in and start a session |
| POST | /api/auth/logout | Logged in | End the session |
| GET | /api/users/profile | Logged in | Get own profile |
| PUT | /api/users/profile | Logged in | Update own profile |
| POST | /api/users/profile/picture | Participant | Upload profile picture |
| GET | /api/events | Public | List events (filters: type, date, location, mine) |
| GET | /api/events/{id} | Public | Event details with categories |
| POST | /api/events | Organiser | Create event |
| PUT | /api/events/{id} | Organiser (owner) | Update event |
| DELETE | /api/events/{id} | Organiser (owner) | Delete event |
| POST | /api/events/{eventId}/upload-banner | Organiser (owner) | Upload banner image |
| GET | /api/events/{eventId}/categories | Public | List categories |
| POST | /api/events/{eventId}/categories | Organiser (owner) | Add category |
| PUT | /api/categories/{id} | Organiser (owner) | Update category |
| DELETE | /api/categories/{id} | Organiser (owner) | Delete category |
| GET | /api/enrolments | Logged in | Organiser: all enrolments for their events. Participant: own enrolments |
| GET | /api/events/{eventId}/enrolments | Organiser (owner) | Enrolments for one event |
| POST | /api/events/{eventId}/enrol | Participant | Enrol in an event by category |
| PUT | /api/enrolments/{id}/status | Organiser (owner) | Set Pending, Confirmed or Cancelled |
| GET | /api/results/me | Participant | Own race history (includes field size) |
| GET | /api/events/{eventId}/results | Organiser (owner) | Results for one event |
| POST | /api/events/{eventId}/results | Organiser (owner) | Capture a finish time and position |
| PUT | /api/results/{id} | Organiser (owner) | Correct a result |

### Deviations from the Part 1 Plan
The API follows the approved plan. These differences were needed or added on purpose, and each is listed here as the brief requires:

1. **`contactNumber`** is sent on register and on profile update for Organisers. The Organisers table requires it, and the plan's body did not include it.
2. **`POST /api/users/profile/picture`** was added. The plan had no endpoint for the profile picture that Part 3 needs.
3. **`GET /api/events?mine=true`** was added so the Organiser dashboard (Part 3) can list only that Organiser's events.
4. **`PUT /api/users/profile`** returns **409** if the new email belongs to another account. The plan listed only 200 and 400.
5. **`DELETE /api/events/{id}`** also removes the event's categories, enrolments and results, in one transaction. The foreign keys would otherwise block the delete.
6. **`DELETE /api/categories/{id}`** returns **409** if participants are already enrolled in the category, so their enrolments are not lost. The plan did not list this.
7. **Enrolment age check:** `POST /api/events/{eventId}/enrol` returns 400 when the participant's age on the event date is outside the category's range. This is the "category invalid" case in the plan.
8. **Results** include `totalFinishers` so the Participant view can show "47th out of 312".
9. **Error body:** every error returns `{ "message": "..." }`.

### Design Notes
- **Schema:** `RaceDayDbContext` maps every table, column name, key, UNIQUE index, CHECK constraint, default, and delete rule to the Part 1 SQL script. The initial migration (`Data/Migrations/`) generates the same tables.
- **Sessions:** login stores `UserId` and `Role` in the server-side session. The `[RequireSession]` attribute returns 401 when there is no session and 403 when the role is wrong. Ownership checks (for example, editing another Organiser's event) return 403.
- **Passwords:** stored only as BCrypt hashes with a random salt. The plain text is never saved.
- **Files:** uploads go through the `IFileStorage` interface. Part 2 saves them under `wwwroot/uploads`. Part 3 will add an Azure Blob implementation without changing the controllers.

### CI/CD

[![Planning document check](https://github.com/YOUR-USERNAME/YOUR-REPOSITORY/actions/workflows/validate.yml/badge.svg?branch=main)](https://github.com/FRANS717/Prog6212-POE-PART-2.git/actions/workflows/validate.yml)

The workflow `.github/workflows/dotnet-build-and-test.yml` runs on every push and pull request. It restores, builds in Release, runs the xUnit suite, and uploads the test results.

**CI green build screenshot:** `docs/screenshots/ci-green-build.png` 

### Video Presentation
- **Part 1 (planning):** https://youtu.be/RrkhZZGbrcM
- **Part 2 (API, authentication, Swagger, unit tests):** *https://youtu.be/aCJvocN834Y?si=DcZ1JwumTEYKDjo4*

### AI Use Disclosure
AI tools (Claude) helped to plan the structure, write parts of the code and tests, and draft this README. Every change was reviewed and run locally. The design choices and the reasons for them are explained in the video.

## Student Declaration
I declare that this work is my own original work and all sources have been properly referenced.
Name: Brain Frans Mashabela
Student Number: ST10476451
Date: 02/09/2026
part 2 declaration 
Date: 15/10/2026
