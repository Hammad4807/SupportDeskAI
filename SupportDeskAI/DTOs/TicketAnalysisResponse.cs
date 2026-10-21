namespace SupportDeskAI.DTOs;

// What POST /api/tickets/{id}/analyze returns (after validation; the same values are saved on the ticket).
public class TicketAnalysisResponse
{
    public Guid TicketId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; } // 0 = Low, 1 = Medium, 2 = High, 3 = Critical
    public string Sentiment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; // "Groq" or "Mock", so a mock result is clearly identified
}
