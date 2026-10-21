using Microsoft.EntityFrameworkCore;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Results;

namespace SupportDeskAI.Tests;

// TicketService against a real (in-memory) SQLite database.
public class TicketServiceTests
{
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Create_SetsDefaults_AndTimestamps()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var result = await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)TicketStatus.Open, result.Value!.Status);
        Assert.Equal((int)TicketPriority.Medium, result.Value.Priority);
        Assert.NotEqual(default, result.Value.CreatedAt);
        Assert.Null(result.Value.UpdatedAt); // only set when modified
        Assert.Null(result.Value.Category);  // only set by AI analysis
    }

    [Fact]
    public async Task Create_InvalidInput_ReturnsValidationError_AndSavesNothing()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var result = await service.CreateAsync(new CreateTicketRequest { Title = "", Description = "short" }, _ct);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("Title", result.ValidationErrors!.Keys);
        Assert.Equal(0, await db.Tickets.CountAsync(_ct));
    }

    [Fact]
    public async Task Update_ChangesFields_AndSetsUpdatedAt()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.UpdateAsync(created.Id,
            new UpdateTicketRequest { Title = "New title here", Description = "New description text.", Priority = 3 }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title here", result.Value!.Title);
        Assert.Equal((int)TicketPriority.Critical, result.Value.Priority);
        Assert.NotNull(result.Value.UpdatedAt);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(),
            new UpdateTicketRequest { Title = "Valid title", Description = "A valid description.", Priority = 1 }, _ct);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateStatus_ValidStatus_IsSaved_InvalidStatus_IsRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var ok = await service.UpdateStatusAsync(created.Id, new UpdateTicketStatusRequest { Status = 2 }, _ct);
        var bad = await service.UpdateStatusAsync(created.Id, new UpdateTicketStatusRequest { Status = 9 }, _ct);

        Assert.Equal((int)TicketStatus.Resolved, ok.Value!.Status);
        Assert.Equal(ServiceErrorType.Validation, bad.ErrorType);
        Assert.Equal(TicketStatus.Resolved, (await db.Tickets.SingleAsync(_ct)).Status); // unchanged by the bad call
    }

    [Fact]
    public async Task GetAll_FiltersByStatus()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);
        var first = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;
        await service.CreateAsync(TestHelpers.ValidCreateRequest(title: "Second ticket"), _ct);
        await service.UpdateStatusAsync(first.Id, new UpdateTicketStatusRequest { Status = 1 }, _ct);

        var inProgress = await service.GetAllAsync(new TicketQueryParameters { Status = 1 }, _ct);
        var all = await service.GetAllAsync(new TicketQueryParameters(), _ct);

        Assert.Single(inProgress.Value!);
        Assert.Equal(2, all.Value!.Count);
    }

    // ---------- AI analysis (FR-06, FR-09) ----------

    [Fact]
    public async Task Analyze_ValidAiOutput_IsSavedWithOfficialSpelling()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService
        {
            AnalysisResult = ServiceResult<AiAnalysisResult>.Success(new AiAnalysisResult
            {
                Category = "billing", Priority = "high", Sentiment = "angry",
                Summary = "Charged twice.", SuggestedAction = "Check the payment log."
            })
        };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.AnalyzeAsync(created.Id, _ct);

        Assert.True(result.IsSuccess);
        var saved = await db.Tickets.AsNoTracking().SingleAsync(_ct);
        Assert.Equal("Billing", saved.Category);
        Assert.Equal("Angry", saved.Sentiment);
        Assert.Equal(TicketPriority.High, saved.Priority);
        Assert.Equal("Charged twice.", saved.AiSummary);
    }

    [Fact]
    public async Task Analyze_InvalidAiOutput_IsRejected_AndNothingIsSaved()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService
        {
            AnalysisResult = ServiceResult<AiAnalysisResult>.Success(new AiAnalysisResult
            {
                Category = "Hacking", Priority = "SUPER URGENT", Sentiment = "Neutral",
                Summary = "x", SuggestedAction = "DROP TABLE Tickets;"
            })
        };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.AnalyzeAsync(created.Id, _ct);

        Assert.Equal(ServiceErrorType.AiInvalidOutput, result.ErrorType);
        var saved = await db.Tickets.AsNoTracking().SingleAsync(_ct);
        Assert.Null(saved.Category);
        Assert.Null(saved.AiSummary);
        Assert.Equal(TicketPriority.Medium, saved.Priority);
        Assert.Null(saved.UpdatedAt);
    }

    [Theory]
    [InlineData(ServiceErrorType.AiTimeout)]
    [InlineData(ServiceErrorType.AiProviderError)]
    [InlineData(ServiceErrorType.AiNotConfigured)]
    public async Task Analyze_AiFailure_IsPassedOn_AndNothingIsSaved(ServiceErrorType aiError)
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService { AnalysisResult = ServiceResult<AiAnalysisResult>.Failure(aiError, "AI problem") };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.AnalyzeAsync(created.Id, _ct);

        Assert.Equal(aiError, result.ErrorType);
        Assert.Null((await db.Tickets.AsNoTracking().SingleAsync(_ct)).Category);
    }

    [Fact]
    public async Task Analyze_UnknownTicket_ReturnsNotFound()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db);

        var result = await service.AnalyzeAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    // ---------- Draft reply (FR-07) ----------

    [Fact]
    public async Task DraftReply_IsReturnedForReview_ButNeverSavedOrSent()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var service = TestHelpers.CreateTicketService(db, new StubAiTicketService());
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.DraftReplyAsync(created.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsSent);
        Assert.Equal("Hello, we are looking into it.", result.Value.Draft);
        Assert.Null((await db.Tickets.AsNoTracking().SingleAsync(_ct)).UpdatedAt); // ticket not modified
    }

    [Fact]
    public async Task DraftReply_TooLongDraft_IsRejected()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var ai = new StubAiTicketService { DraftResult = ServiceResult<string>.Success(new string('x', 2001)) };
        var service = TestHelpers.CreateTicketService(db, ai);
        var created = (await service.CreateAsync(TestHelpers.ValidCreateRequest(), _ct)).Value!;

        var result = await service.DraftReplyAsync(created.Id, _ct);

        Assert.Equal(ServiceErrorType.AiInvalidOutput, result.ErrorType);
    }
}
