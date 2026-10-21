namespace SupportDeskAI.DTOs;

// Body of PUT /api/tickets/{id}. These are the fields an agent is allowed to change.
// Status has its own endpoint (PATCH /status); Id and timestamps are never changed by the client.
public class UpdateTicketRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } // 0 = Low, 1 = Medium, 2 = High, 3 = Critical
}
