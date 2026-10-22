namespace SupportDeskAI.Mcp.DTOs;

// Result of analyze_ticket. Same data as the API's TicketAnalysisResponse,
// with Category and Priority as names instead of numbers.
public class TicketAnalysisToolResult
{
    public Guid TicketId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; // "Groq" or "Mock"
}
