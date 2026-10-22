namespace SupportDeskAI.DTOs;

// Optional filters for GET /api/tickets, e.g. /api/tickets?status=0&priority=2&category=1
public class TicketQueryParameters
{
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public int? Category { get; set; }
}
