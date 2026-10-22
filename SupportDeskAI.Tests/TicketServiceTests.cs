using System.Net;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;

namespace SupportDeskAI.Tests;

// TicketService against a real (in-memory) SQLite database.
// Expected failures are thrown as built-in exceptions, so the tests check which exception is thrown.
public class TicketServiceTests
{
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Create_SetsDefaults_AndTimestamps()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var ticket = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        Assert.Equal((int)TicketStatus.Open, ticket.Status);
        Assert.Equal((int)TicketPriority.Medium, ticket.Priority);
        Assert.NotEqual(default, ticket.CreatedAt);
        Assert.Null(ticket.UpdatedAt); // only set when modified
        Assert.Null(ticket.Category);  // only set by AI analysis
    }

    [Fact]
    public async Task Create_InvalidInput_ThrowsValidationException_AndSavesNothing()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateTicketRequest { Title = "", Description = "short" }, _ct));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Title");
        Assert.Equal(0, await db.Tickets.CountAsync(_ct));
    }

    [Fact]
    public async Task GetById_UnknownId_ThrowsKeyNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), _ct));
    }

    [Fact]
    public async Task Update_ChangesFields_AndSetsUpdatedAt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        var updated = await service.UpdateAsync(created.Id,
            new UpdateTicketRequest { Title = "New title here", Description = "New description text.", Priority = 3 }, _ct);

        Assert.Equal("New title here", updated.Title);
        Assert.Equal((int)TicketPriority.Critical, updated.Priority);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsKeyNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(Guid.NewGuid(),
            new UpdateTicketRequest { Title = "Valid title", Description = "A valid description.", Priority = 1 }, _ct));
    }

    [Fact]
    public async Task UpdateStatus_ValidStatus_IsSaved_InvalidStatus_IsRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        var ok = await service.UpdateStatusAsync(created.Id, new UpdateTicketStatusRequest { Status = 2 }, _ct);
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStatusAsync(created.Id, new UpdateTicketStatusRequest { Status = 9 }, _ct));

        Assert.Equal((int)TicketStatus.Resolved, ok.Status);
        Assert.Equal(TicketStatus.Resolved, (await db.Tickets.SingleAsync(_ct)).Status); // unchanged by the bad call
    }

    [Fact]
    public async Task GetAll_FiltersByStatus_AndRejectsInvalidFilter()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var first = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);
        await service.CreateAsync(TestHelpers.ValidCreateRequest(title: "Second ticket"), _ct);
        await service.UpdateStatusAsync(first.Id, new UpdateTicketStatusRequest { Status = 1 }, _ct);

        var inProgress = await service.GetAllAsync(new TicketQueryParameters { Status = 1 }, _ct);
        var all = await service.GetAllAsync(new TicketQueryParameters(), _ct);

        Assert.Single(inProgress);
        Assert.Equal(2, all.Count);
        await Assert.ThrowsAsync<ValidationException>(() => service.GetAllAsync(new TicketQueryParameters { Priority = 9 }, _ct));
    }

    [Fact]
    public async Task GetAll_FiltersByCategory_AndRejectsUnknownCategory()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db); // Mock AI: password ticket -> Account Access
        var analyzed = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);
        await service.CreateAsync(TestHelpers.ValidCreateRequest(title: "Second ticket"), _ct);
        await service.AnalyzeAsync(analyzed.Id, _ct);

        var accountAccess = await service.GetAllAsync(new TicketQueryParameters { Category = (int)TicketCategory.AccountAccess }, _ct);
        var billing = await service.GetAllAsync(new TicketQueryParameters { Category = (int)TicketCategory.Billing }, _ct);

        Assert.Equal(analyzed.Id, Assert.Single(accountAccess).Id);
        Assert.Empty(billing);
        await Assert.ThrowsAsync<ValidationException>(() => service.GetAllAsync(new TicketQueryParameters { Category = 99 }, _ct));
    }

    // ---------- AI analysis (FR-06, FR-09) ----------

    [Fact]
    public async Task Analyze_ValidAiOutput_IsSavedWithOfficialSpelling()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService
        {
            Analysis = new AiAnalysisResult
            {
                Category = "billing", Priority = "high", Sentiment = "angry",
                Summary = "Charged twice.", SuggestedAction = "Check the payment log."
            }
        };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        var result = await service.AnalyzeAsync(created.Id, _ct);

        Assert.Equal("Stub", result.Provider);
        var saved = await db.Tickets.AsNoTracking().SingleAsync(_ct);
        Assert.Equal(TicketCategory.Billing, saved.Category);           // "billing" -> enum
        Assert.Equal((int)TicketCategory.Billing, result.Category);     // API returns the number
        Assert.Equal("Angry", saved.Sentiment);
        Assert.Equal(TicketPriority.High, saved.Priority);
        Assert.Equal("Charged twice.", saved.AiSummary);
    }

    [Fact]
    public async Task Analyze_InvalidAiOutput_ThrowsInvalidData_AndNothingIsSaved()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService
        {
            Analysis = new AiAnalysisResult
            {
                Category = "Hacking", Priority = "SUPER URGENT", Sentiment = "Neutral",
                Summary = "x", SuggestedAction = "DROP TABLE Tickets;"
            }
        };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.AnalyzeAsync(created.Id, _ct));

        var saved = await db.Tickets.AsNoTracking().SingleAsync(_ct);
        Assert.Null(saved.Category);
        Assert.Null(saved.AiSummary);
        Assert.Equal(TicketPriority.Medium, saved.Priority);
        Assert.Null(saved.UpdatedAt);
    }

    public static TheoryData<Exception> AiFailures => new()
    {
        new TimeoutException("The AI did not answer in time."),                                       // 504
        new HttpRequestException("The AI provider failed.", null, HttpStatusCode.BadGateway),         // 502
        new HttpRequestException("GROQ_API_KEY is not set.", null, HttpStatusCode.ServiceUnavailable) // 503
    };

    [Theory]
    [MemberData(nameof(AiFailures))]
    public async Task Analyze_AiFailure_IsPassedOn_AndNothingIsSaved(Exception aiFailure)
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db, new StubAiTicketService { ExceptionToThrow = aiFailure });
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => service.AnalyzeAsync(created.Id, _ct));

        Assert.Same(aiFailure, thrown);
        Assert.Null((await db.Tickets.AsNoTracking().SingleAsync(_ct)).Category);
    }

    [Fact]
    public async Task Analyze_UnknownTicket_ThrowsKeyNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AnalyzeAsync(Guid.NewGuid(), _ct));
    }

    // ---------- Draft reply (FR-07) ----------

    [Fact]
    public async Task DraftReply_IsReturnedForReview_ButNeverSavedOrSent()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db, new StubAiTicketService());
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        var result = await service.DraftReplyAsync(created.Id, _ct);

        Assert.False(result.IsSent);
        Assert.Equal("Hello, we are looking into it.", result.Draft);
        Assert.Null((await db.Tickets.AsNoTracking().SingleAsync(_ct)).UpdatedAt); // ticket not modified
    }

    [Fact]
    public async Task DraftReply_TooLongDraft_ThrowsInvalidData()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db, new StubAiTicketService { Draft = new string('x', 2001) });
        var created = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DraftReplyAsync(created.Id, _ct));
    }
}
