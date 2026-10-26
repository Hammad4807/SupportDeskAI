using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Data;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Services;
using SupportDeskAI.Validators;

namespace SupportDeskAI.Configuration;

// The application services shared by BOTH programs: the Web API (SupportDeskAI) and the MCP server
// (SupportDeskAI.Mcp). Registering them in one place guarantees that REST endpoints and MCP tools
// use exactly the same database, business logic, validation and AI provider.
// Web-only things (controllers, Swagger, CORS, exception handler) stay in the API's Program.cs.
public static class ServiceRegistration
{
    public static void AddSupportDeskServices(IServiceCollection services, IConfiguration configuration)
    {
        // Database: SQLite file, path comes from appsettings.json ("ConnectionStrings:DefaultConnection").
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        // Business logic: one instance per request (per HTTP request in the API, per tool call in MCP).
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // AI provider (FR-06/07/10): "Ai:Provider" picks the real Groq service or the Mock.
        // The Groq API key is read from user-secrets / environment variables, never from appsettings.json.
        services.AddHttpClient<GroqAiTicketService>(); // HttpClient comes from IHttpClientFactory
        services.AddScoped<MockAiTicketService>();
        services.AddScoped<IAiTicketService>(sp =>
            configuration["Ai:Provider"] == "Mock"
                ? sp.GetRequiredService<MockAiTicketService>()
                : sp.GetRequiredService<GroqAiTicketService>());

        // Every FluentValidation validator in the API project (CreateTicketRequestValidator, ...).
        services.AddValidatorsFromAssemblyContaining<CreateTicketRequestValidator>();
    }
}
