using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDeskAI.Data;
using SupportDeskAI.DTOs;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Services;
using SupportDeskAI.Validators;

namespace SupportDeskAI.Tests;

// Shared setup for the service tests.
public static class TestHelpers
{
    // A real SQLite database that lives only in memory while the connection is open,
    // so every test starts with an empty database and no file is created.
    public static AppDbContext CreateInMemoryDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated(); // creates the tables from the model (no migration needed for tests)
        return db;
    }

    // TicketService with the real validators and the given AI service.
    public static TicketService CreateTicketService(AppDbContext db, IAiTicketService? ai = null)
    {
        return new TicketService(
            db,
            new CreateTicketRequestValidator(),
            new UpdateTicketRequestValidator(),
            new UpdateTicketStatusRequestValidator(),
            new TicketQueryParametersValidator(),
            new AiAnalysisResultValidator(),
            ai ?? new MockAiTicketService(),
            NullLogger<TicketService>.Instance);
    }

    public static CreateTicketRequest ValidCreateRequest(
        string title = "Unable to access my account",
        string description = "I reset my password but still cannot sign in.") =>
        new() { Title = title, Description = description };
}

// A fake AI whose answer each test chooses: a value to return, or an exception to throw (timeout, ...).
public class StubAiTicketService : IAiTicketService
{
    public AiAnalysisResult? Analysis { get; set; }
    public string Draft { get; set; } = "Hello, we are looking into it.";
    public Exception? ExceptionToThrow { get; set; }

    public string ProviderName => "Stub";

    public Task<AiAnalysisResult> AnalyzeTicketAsync(string title, string description, CancellationToken ct) =>
        ExceptionToThrow is not null ? Task.FromException<AiAnalysisResult>(ExceptionToThrow) : Task.FromResult(Analysis!);

    public Task<string> DraftReplyAsync(string title, string description, string? category, CancellationToken ct) =>
        ExceptionToThrow is not null ? Task.FromException<string>(ExceptionToThrow) : Task.FromResult(Draft);
}
