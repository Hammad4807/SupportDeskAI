namespace SupportDeskAI.DTOs;

// Body of POST /api/tickets. Validated by CreateTicketRequestValidator.
// Id, Status, Priority and timestamps are decided by the server, not the client.
public class CreateTicketRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
