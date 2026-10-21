using SupportDeskAI.DTOs;

namespace SupportDeskAI.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(CancellationToken ct);
}
