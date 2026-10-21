# SupportDesk AI – API

A support-ticket management API built with **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core** and **SQLite**.
Agents can create, view, update, filter and close tickets. An AI service (Groq, or a built-in mock) can
**analyze** a ticket (category, priority, sentiment, summary, suggested action) and **draft a reply** for an agent
to review. **AI replies are never sent automatically.**

The optional React frontend lives in a separate repository: **SupportDeskAI-Web**.

---

## Contents

- [Prerequisites](#prerequisites)
- [Setup and run](#setup-and-run)
- [Configuration](#configuration)
- [Database and migrations](#database-and-migrations)
- [Endpoints](#endpoints)
- [Error responses](#error-responses)
- [Tests](#tests)
- [Project structure](#project-structure)
- [How a request flows](#how-a-request-flows)
- [Known limitations](#known-limitations)

---

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| EF Core tools | 10.x | `dotnet tool install --global dotnet-ef` (once) |
| Groq API key | optional | Free key from https://console.groq.com/keys. Without it, use the **Mock** AI provider. |

No database server is needed: SQLite stores everything in one file (`supportdesk.db`).

---

## Setup and run

```powershell
git clone <this-repository-url>
cd SupportDeskAI/SupportDeskAI          # the folder that contains SupportDeskAI.csproj

# 1. Create the database (creates supportdesk.db from the migrations)
dotnet ef database update

# 2. Configure the AI (choose one)
dotnet user-secrets set "GROQ_API_KEY" "your-groq-api-key-here"   # real AI
#   or open appsettings.json and set  "Ai": { "Provider": "Mock" }  # no key needed

# 3. Run
dotnet run --launch-profile http
```

Open **http://localhost:5249/swagger** to try every endpoint.
In Visual Studio: open `SupportDeskAI.slnx`, choose the **http** profile and press **F5** (Swagger opens automatically).

---

## Configuration

`appsettings.json` contains **no secrets**:

```json
"ConnectionStrings": { "DefaultConnection": "Data Source=supportdesk.db" },
"Cors":  { "AllowedOrigins": [ "http://localhost:5173" ] },
"Ai":    { "Provider": "Groq", "TimeoutSeconds": 30 },
"Groq":  { "Model": "openai/gpt-oss-120b", "MaxCompletionTokens": 1024 }
```

| Setting | Meaning |
|---|---|
| `Ai:Provider` | `Groq` = real AI, `Mock` = built-in fake AI (no key, no internet, output marked `[MOCK]`) |
| `Ai:TimeoutSeconds` | AI calls are cancelled after this many seconds and the API returns **504** |
| `Groq:Model` | Groq model name |
| `GROQ_API_KEY` | **Secret.** Set it with user-secrets or an environment variable, never in a committed file |

A configuration example with placeholder values is in [`.env.example`](.env.example).
Environment variables override `appsettings.json` (use `__` for nesting, e.g. `Ai__Provider=Mock`).

---

## Database and migrations

The schema is created by EF Core migrations in `SupportDeskAI/Migrations`.

```powershell
cd SupportDeskAI
dotnet ef database update                 # create / update supportdesk.db
dotnet ef migrations add <Name>           # after changing an entity
dotnet ef migrations list                 # see which migrations exist / are applied
```

To start with an empty database: stop the API, delete `supportdesk.db*`, run `dotnet ef database update`.
The `.db` file is ignored by Git, so every clone creates its own database.

**Ticket fields:** `Id` (GUID), `Title`, `Description`, `Status` (0 Open, 1 InProgress, 2 Resolved, 3 Closed),
`Priority` (0 Low, 1 Medium, 2 High, 3 Critical), `Category`, `Sentiment`, `AiSummary`, `SuggestedAction`
(null until analyzed), `CreatedAt`, `UpdatedAt` (UTC; `UpdatedAt` is null until the ticket is modified).

---

## Endpoints

All examples are also in [`SupportDeskAI/SupportDeskAI.http`](SupportDeskAI/SupportDeskAI.http) (run them from Visual Studio / VS Code).

| # | Method | URL | Success | Errors |
|---|---|---|---|---|
| FR-01 | POST | `/api/tickets` | 201 | 400 |
| FR-02 | GET | `/api/tickets?status=&priority=&category=` | 200 | 400 (invalid filter) |
| FR-03 | GET | `/api/tickets/{id}` | 200 | 404 |
| FR-04 | PUT | `/api/tickets/{id}` | 200 | 400, 404 |
| FR-05 | PATCH | `/api/tickets/{id}/status` | 200 | 400, 404 |
| FR-06 | POST | `/api/tickets/{id}/analyze` | 200 | 404, 502, 503, 504 |
| FR-07 | POST | `/api/tickets/{id}/draft-reply` | 200 | 404, 502, 503, 504 |
| FR-08 | GET | `/api/dashboard/summary` | 200 | – |

**Create a ticket**

```http
POST /api/tickets
{ "title": "Unable to access my account", "description": "I reset my password but still cannot sign in." }
```
```json
{ "id": "3f2b8c1e-4a7d-4c1a-9b2e-0f6d5a1c2b3d", "title": "Unable to access my account",
  "description": "I reset my password but still cannot sign in.", "status": 0, "priority": 1,
  "category": null, "sentiment": null, "aiSummary": null, "suggestedAction": null,
  "createdAt": "2026-10-21T10:00:00Z", "updatedAt": null }
```

**Update / change status**

```http
PUT /api/tickets/{id}            { "title": "...", "description": "...", "priority": 2 }
PATCH /api/tickets/{id}/status   { "status": 1 }
```

**AI analysis** – the answer is validated (allowed categories, priorities and sentiments) and only then saved on the ticket:

```json
{ "ticketId": "3f2b8c1e-...", "category": "Account Access", "priority": 2, "sentiment": "Frustrated",
  "summary": "The customer cannot sign in after resetting their password.",
  "suggestedAction": "Check account status and the password reset flow.", "provider": "Groq" }
```

Allowed categories: Account Access, Billing, Technical Issue, Bug Report, Feature Request, General Inquiry, Other.
Allowed sentiments: Positive, Neutral, Negative, Frustrated, Angry.

**Draft reply** – returned for human review, never saved or sent:

```json
{ "ticketId": "3f2b8c1e-...", "draft": "Hello, thank you for contacting us ...", "provider": "Groq",
  "isSent": false, "notice": "AI-generated draft. Review and edit it before sending. It has not been sent.",
  "generatedAt": "2026-10-21T10:05:00Z" }
```

**Dashboard**

```json
{ "total": 12, "open": 5, "inProgress": 3, "resolved": 3, "closed": 1, "highPriority": 4, "unanalyzed": 2 }
```

---

## Error responses

Every error is a [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) JSON object (`title`, `status`, `detail`, `traceId`).
Stack traces and internal messages are only written to the server log.

| Status | When |
|---|---|
| 400 | Validation failed (`errors` lists the messages per field) |
| 404 | Ticket not found |
| 500 | Unexpected error (caught by `UseExceptionHandler` + `GlobalExceptionHandler`) |
| 502 | AI provider error, or AI output was malformed / used values that are not allowed (nothing is saved) |
| 503 | AI not configured (`GROQ_API_KEY` missing) |
| 504 | AI did not answer within `Ai:TimeoutSeconds` |

Ticket create / read / update / status and the dashboard keep working when the AI provider is unavailable.

---

## Tests

```powershell
cd SupportDeskAI        # the folder that contains SupportDeskAI.slnx
dotnet test
```

The xUnit project `SupportDeskAI.Tests` uses an in-memory SQLite database and a stub AI. It covers:

- request validation (title/description limits, invalid status and priority values);
- AI output validation (malformed JSON, values outside the allowed lists, mock output is valid);
- personal-data redaction before text is sent to the AI provider;
- `TicketService`: defaults and timestamps, update/status, filters, AI analysis saved only when valid,
  AI timeouts and provider errors save nothing, draft replies are never saved;
- dashboard counts.

---

## Project structure

```
SupportDeskAI.slnx
SupportDeskAI/                      ASP.NET Core Web API
  Controllers/                      Thin HTTP layer (TicketsController, DashboardController)
  Services/                         Business logic (TicketService, DashboardService) and AI providers
                                    (GroqAiTicketService, MockAiTicketService)
  Interfaces/                       ITicketService, IDashboardService, IAiTicketService
  Data/                             AppDbContext (EF Core, automatic CreatedAt/UpdatedAt)
  Models/                           Ticket entity
  Enums/                            TicketStatus, TicketPriority, ServiceErrorType
  DTOs/                             Request/response shapes (entities are never returned directly)
  Validators/                       FluentValidation rules (requests and AI output)
  Results/                          ServiceResult<T>: success value or error type, without exceptions
  Constants/                        Allowed AI values
  Helpers/                          AiResponseParser, PersonalDataRedactor
  Middlewares/                      RequestLoggingMiddleware
  ExceptionHandlers/                GlobalExceptionHandler (IExceptionHandler, .NET 8+)
  Migrations/                       EF Core migrations
SupportDeskAI.Tests/                xUnit tests
```

## How a request flows

```
HTTP request
  -> CORS -> RequestLoggingMiddleware -> UseExceptionHandler (GlobalExceptionHandler)
  -> Controller            (receives the DTO, calls the service, maps ServiceResult to a status code)
  -> TicketService         (validates input, applies business rules)
       -> AppDbContext      -> SQLite
       -> IAiTicketService  -> Groq or Mock   (answer parsed, validated, then saved)
```

---

## Known limitations

- **MCP server (FR-11 to FR-13) is not implemented yet.** The service layer already validates input itself,
  so MCP tools can reuse `ITicketService` without duplicating business logic.
- No authentication or authorization (out of scope for this project).
- `PersonalDataRedactor` only removes emails and long numbers (phones, card numbers); names and addresses are
  still sent to the AI provider, so only test data should be used.
- The AI can still choose a wrong category or priority within the allowed values; agents can correct them with `PUT`.
- SQLite is meant for local development; it is not suited to many concurrent users.
