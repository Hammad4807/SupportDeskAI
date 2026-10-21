namespace SupportDeskAI.DTOs;

// Optional filters for GET /api/tickets, e.g. /api/tickets?status=0&priority=2&category=Billing
public class TicketQueryParameters
{
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public string? Category { get; set; }
}
