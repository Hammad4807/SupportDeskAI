namespace SupportDeskAI.DTOs;

// Body of PATCH /api/tickets/{id}/status
public class UpdateTicketStatusRequest
{
    public int Status { get; set; } // 0 = Open, 1 = InProgress, 2 = Resolved, 3 = Closed
}
