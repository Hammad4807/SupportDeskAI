namespace SupportDeskAI.DTOs;

// What the API returns for a ticket.
// Status/Priority are the stored numbers (0..3); the frontend maps them to names.
public class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int Status { get; set; }
    public int Priority { get; set; }

    // AI analysis results; null until the ticket has been analyzed.
    public int? Category { get; set; }
    public string? Sentiment { get; set; }
    public string? AiSummary { get; set; }
    public string? SuggestedAction { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
