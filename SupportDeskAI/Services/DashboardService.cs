using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Data;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;

namespace SupportDeskAI.Services;

// Counts are calculated by the database (SQL COUNT / GROUP BY), not by loading every ticket into memory.
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        // One query: how many tickets per status, e.g. { Open: 5, InProgress: 2 }
        var countsByStatus = await _db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        int highPriority = await _db.Tickets
            .CountAsync(t => t.Priority == TicketPriority.High || t.Priority == TicketPriority.Critical, ct);

        int unanalyzed = await _db.Tickets.CountAsync(t => t.Category == null, ct);

        return new DashboardSummaryResponse
        {
            Total = countsByStatus.Values.Sum(),
            Open = countsByStatus.GetValueOrDefault(TicketStatus.Open),
            InProgress = countsByStatus.GetValueOrDefault(TicketStatus.InProgress),
            Resolved = countsByStatus.GetValueOrDefault(TicketStatus.Resolved),
            Closed = countsByStatus.GetValueOrDefault(TicketStatus.Closed),
            HighPriority = highPriority,
            Unanalyzed = unanalyzed
        };
    }
}
