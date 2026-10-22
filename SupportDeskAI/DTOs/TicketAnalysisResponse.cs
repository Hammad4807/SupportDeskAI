namespace SupportDeskAI.DTOs;

// What POST /api/tickets/{id}/analyze returns (after validation; the same values are saved on the ticket).
public class TicketAnalysisResponse
{
    public Guid TicketId { get; set; }
    public int Category { get; set; }
    public int Priority { get; set; } 
    public string Sentiment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; 
}
