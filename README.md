# SupportDesk AI – API

A support-ticket management API built with **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core** and **SQLite**.
Agents can create, view, update, filter and close tickets. An AI service (Groq, or a built-in mock) can
**analyze** a ticket (category, priority, sentiment, summary, suggested action) and **draft a reply** for an agent
to review. **AI replies are never sent automatically.**

The same ticket operations are also exposed as an **MCP server** (`SupportDeskAI.Mcp`), so any MCP-compatible
LLM client (Claude Desktop, MCP Inspector, ...) can use them.

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
- [MCP server](#mcp-server)
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
| Node.js | 20+ (only for MCP Inspector) | `node --version` |

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
`Priority` (0 Low, 1 Medium, 2 High, 3 Critical),
`Category` (0 Account Access, 1 Billing, 2 Technical Issue, 3 Bug Report, 4 Feature Request, 5 General Inquiry, 6 Other),
`Sentiment`, `AiSummary`, `SuggestedAction` (null until analyzed), `CreatedAt`, `UpdatedAt`
(UTC; `UpdatedAt` is null until the ticket is modified). Status, Priority and Category are enums stored and sent as numbers.

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
{ "ticketId": "3f2b8c1e-...", "category": 0, "priority": 2, "sentiment": "Frustrated",
  "summary": "The customer cannot sign in after resetting their password.",
  "suggestedAction": "Check account status and the password reset flow.", "provider": "Groq" }
```

The AI answers with a category name (Account Access, Billing, Technical Issue, Bug Report, Feature Request,
General Inquiry, Other); the API checks it and stores the matching `TicketCategory` number (0–6).
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

Services throw **built-in .NET exceptions**; `GlobalExceptionHandler` (an `IExceptionHandler`, run by
`app.UseExceptionHandler()`) is the one place that turns them into status codes:

| Status | When | Exception |
|---|---|---|
| 400 | Validation failed (`errors` lists the messages per field) | `FluentValidation.ValidationException` |
| 404 | Ticket not found | `KeyNotFoundException` |
| 502 | AI provider error | `HttpRequestException` (StatusCode 502) |
| 502 | AI output was malformed or used values that are not allowed (nothing is saved) | `InvalidDataException` |
| 503 | AI not configured (`GROQ_API_KEY` missing) | `HttpRequestException` (StatusCode 503) |
| 504 | AI did not answer within `Ai:TimeoutSeconds` | `TimeoutException` |
| 500 | Any other, unexpected error (generic message) | anything else |

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

## MCP server

`SupportDeskAI.Mcp` is a separate console project in the solution. It uses the official C# MCP SDK
(`ModelContextProtocol` NuGet package) with the **stdio** transport: the MCP client starts the program and talks to it
over stdin/stdout. It registers the **same services** as the API (`Configuration/ServiceRegistration.cs`), so every
tool calls the same `TicketService` / `DashboardService` as the REST endpoints, with the same validation.

### Tools

| Tool | Parameters | Same as |
|---|---|---|
| `create_ticket` | `title`, `description` | `POST /api/tickets` |
| `list_tickets` | optional `status`, `priority`, `category` | `GET /api/tickets` |
| `get_ticket` | `id` | `GET /api/tickets/{id}` |
| `update_ticket` | `id`, `title`, `description`, `priority` | `PUT /api/tickets/{id}` |
| `update_ticket_status` | `id`, `status` | `PATCH /api/tickets/{id}/status` |
| `analyze_ticket` | `id` | `POST /api/tickets/{id}/analyze` |
| `draft_reply` | `id` | `POST /api/tickets/{id}/draft-reply` (draft only, never sent) |
| `get_dashboard_summary` | – | `GET /api/dashboard/summary` |

Values are names, not numbers: status `Open | InProgress | Resolved | Closed`, priority `Low | Medium | High | Critical`,
category `Account Access | Billing | Technical Issue | Bug Report | Feature Request | General Inquiry | Other`.
There is no delete tool. Invalid arguments, unknown ids and AI problems are returned as tool errors
(`isError: true`) with a short message, never with a stack trace.

### Setup

1. Create the database once (see [Database and migrations](#database-and-migrations)). The MCP server uses the
   **same** `SupportDeskAI/supportdesk.db` as the API (set in `SupportDeskAI.Mcp/appsettings.json`).
2. The AI key is shared with the API: both projects use the same user-secrets id, so
   `dotnet user-secrets set "GROQ_API_KEY" "..."` in the `SupportDeskAI` folder is enough.
   Without a key, set `"Ai": { "Provider": "Mock" }` in `SupportDeskAI.Mcp/appsettings.json`.
3. Build it (stop the API first if it is running, otherwise its files are locked):
   ```powershell
   cd SupportDeskAI          # the folder that contains SupportDeskAI.slnx
   dotnet build SupportDeskAI.Mcp
   ```
   The server is `SupportDeskAI.Mcp\bin\Debug\net10.0\SupportDeskAI.Mcp.exe`.

### Connect with MCP Inspector

```powershell
npx @modelcontextprotocol/inspector "C:\full\path\to\SupportDeskAI.Mcp\bin\Debug\net10.0\SupportDeskAI.Mcp.exe"
```
Open the URL it prints, click **Connect**, then **Tools → List Tools**, choose a tool (e.g. `list_tickets`,
`analyze_ticket`), fill in the arguments and click **Run Tool**.

Command-line check without the browser:
```powershell
npx @modelcontextprotocol/inspector --cli "C:\full\path\to\SupportDeskAI.Mcp.exe" --method tools/list
npx @modelcontextprotocol/inspector --cli "C:\full\path\to\SupportDeskAI.Mcp.exe" --method tools/call --tool-name get_dashboard_summary
```

### Connect with Claude Desktop

Open **Settings → Developer → Edit Config** (file `%APPDATA%\Claude\claude_desktop_config.json`), add the server
from [`SupportDeskAI.Mcp/mcp-client-config.example.json`](SupportDeskAI.Mcp/mcp-client-config.example.json) with your
full path to the `.exe`, and restart Claude Desktop:

```json
{
  "mcpServers": {
    "supportdesk-ai": {
      "command": "C:\\path\\to\\SupportDeskAI\\SupportDeskAI.Mcp\\bin\\Debug\\net10.0\\SupportDeskAI.Mcp.exe",
      "args": [],
      "env": { "Ai__Provider": "Groq" }
    }
  }
}
```

Then ask, for example: *"List the open SupportDesk tickets and analyze the newest one."*

Any setting can be overridden in `env`, e.g. `"Ai__Provider": "Mock"` or
`"ConnectionStrings__DefaultConnection": "Data Source=C:\\path\\to\\supportdesk.db"`.

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
  Enums/                            TicketStatus, TicketPriority
  DTOs/                             Request/response shapes (entities are never returned directly)
  Validators/                       FluentValidation rules (requests and AI output)
  Constants/                        Allowed AI values
  Helpers/                          AiResponseParser, PersonalDataRedactor
  Middlewares/                      RequestLoggingMiddleware, GlobalExceptionHandler (IExceptionHandler, .NET 8+)
  Configuration/                    ServiceRegistration: services shared by the API and the MCP server
  Migrations/                       EF Core migrations
SupportDeskAI.Mcp/                  MCP server (stdio), references SupportDeskAI
  Program.cs                        Host, settings, logs to stderr, same services, MCP server + tools
  Tools/TicketTools.cs              The 8 MCP tools (argument checks + one service call each)
  DTOs/                             Tool results with names instead of numbers
  appsettings.json                  Database path (the API's supportdesk.db) and AI settings
  mcp-client-config.example.json   Sample MCP client configuration
SupportDeskAI.Tests/                xUnit tests
```

## How a request flows

```
HTTP request
  -> CORS -> RequestLoggingMiddleware -> UseExceptionHandler (GlobalExceptionHandler)
  -> Controller            (receives the DTO, calls the service, returns 200/201)
  -> TicketService         (validates input, applies business rules; throws built-in exceptions on failure,
                            which GlobalExceptionHandler turns into 400/404/502/503/504)
       -> AppDbContext      -> SQLite
       -> IAiTicketService  -> Groq or Mock   (answer parsed, validated, then saved)
```

---

## Known limitations

- The MCP server supports the stdio transport only (streamable HTTP is optional in the requirements);
  no MCP authentication, remote hosting or multi-user sessions (out of scope).
- No authentication or authorization (out of scope for this project).
- `PersonalDataRedactor` only removes emails and long numbers (phones, card numbers); names and addresses are
  still sent to the AI provider, so only test data should be used.
- The AI can still choose a wrong category or priority within the allowed values; agents can correct them with `PUT`.
- SQLite is meant for local development; it is not suited to many concurrent users.
