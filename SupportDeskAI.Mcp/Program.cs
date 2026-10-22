using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportDeskAI.Configuration;
using SupportDeskAI.Mcp.Tools;

// SupportDesk AI MCP server (FR-11).
// An MCP client (Claude Desktop, MCP Inspector) starts this program and talks to it over
// stdin/stdout using JSON-RPC messages (the "stdio" transport).

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // The client may start us from any folder, so read appsettings.json from the .exe folder.
    ContentRootPath = AppContext.BaseDirectory
});

// GROQ_API_KEY from the same user-secrets as the API (shared UserSecretsId in the .csproj).
// Loaded always, not only in Development, because MCP clients do not set an environment.
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
// Environment variables still win (e.g. "ConnectionStrings__DefaultConnection" set in the client config).
builder.Configuration.AddEnvironmentVariables();

// SQLite resolves a relative "Data Source" from the CURRENT folder, which is unpredictable here.
// Turn it into a full path based on the .exe folder, so it always finds the API's supportdesk.db.
var sqlite = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
if (!Path.IsPathRooted(sqlite.DataSource))
    sqlite.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, sqlite.DataSource));
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = sqlite.ToString()
});

// stdout carries the MCP protocol messages, so ALL logs must go to stderr
// (a single log line on stdout would break the connection with the client).
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// The SAME database, services, AI provider and validators as the Web API.
ServiceRegistration.AddSupportDeskServices(builder.Services, builder.Configuration);

// The MCP server itself (official C# SDK), talking over stdin/stdout.
// ScopeRequests: every tool call gets its own DI scope, so its own AppDbContext (like one HTTP request).
// WithTools<TicketTools>: exposes ONLY the tools in TicketTools (FR-12), nothing else (no delete).
builder.Services
    .AddMcpServer(options => options.ScopeRequests = true)
    .WithStdioServerTransport()
    .WithTools<TicketTools>();

await builder.Build().RunAsync();
