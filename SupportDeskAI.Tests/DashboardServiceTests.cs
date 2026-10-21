using SupportDeskAI.DTOs;
using SupportDeskAI.Services;

namespace SupportDeskAI.Tests;

public class DashboardServiceTests
{
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task EmptyDatabase_ReturnsZeros()
    {
        using var db = TestHelpers.CreateInMemoryDb();

        var summary = await new DashboardService(db).GetSummaryAsync(_ct);

        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.HighPriority);
    }

    [Fact]
    public async Task CountsTicketsByStatusAndPriority()
    {
        using var db = TestHelpers.CreateInMemoryDb();
        var tickets = TestHelpers.CreateTicketService(db); // Mock AI: password ticket -> High
        var a = await tickets.CreateAsync(TestHelpers.ValidCreateRequest(), _ct);
        var b = await tickets.CreateAsync(TestHelpers.ValidCreateRequest(title: "Question about plans", description: "Which plan includes team support?"), _ct);
        await tickets.CreateAsync(TestHelpers.ValidCreateRequest(title: "Third ticket"), _ct);

        await tickets.AnalyzeAsync(a.Id, _ct);
        await tickets.UpdateStatusAsync(b.Id, new UpdateTicketStatusRequest { Status = 2 }, _ct);

        var summary = await new DashboardService(db).GetSummaryAsync(_ct);

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.Open);
        Assert.Equal(1, summary.Resolved);
        Assert.Equal(1, summary.HighPriority);
        Assert.Equal(2, summary.Unanalyzed);
    }
}
