namespace SupportDeskAI.Mcp.DTOs;

// Result of list_tickets. MCP structured results must be a JSON object, not a bare array,
// so the list is wrapped together with its count.
public class TicketListToolResult
{
    public int Count { get; set; }
    public List<TicketToolResult> Tickets { get; set; } = [];
}
