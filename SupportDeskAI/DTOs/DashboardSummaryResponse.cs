namespace SupportDeskAI.DTOs;

// What GET /api/dashboard/summary returns: ticket counts for a quick overview.
public class DashboardSummaryResponse
{
    public int Total { get; set; }
    public int Open { get; set; }
    public int InProgress { get; set; }
    public int Resolved { get; set; }
    public int Closed { get; set; }
    public int HighPriority { get; set; } // priority High or Critical, any status
    public int Unanalyzed { get; set; }   // not analyzed by AI yet (no category)
}
