namespace SupportDeskAI.Mcp.DTOs;

// A ticket as returned by the MCP tools. Same data as the API's TicketResponse, but Status, Priority
// and Category are names ("InProgress", "High", "Account Access") instead of numbers, because an
// LLM reads names more reliably than codes.
public class TicketToolResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Sentiment { get; set; }
    public string? AiSummary { get; set; }
    public string? SuggestedAction { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
