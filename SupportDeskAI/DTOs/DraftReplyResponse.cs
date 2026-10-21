namespace SupportDeskAI.DTOs;

// What POST /api/tickets/{id}/draft-reply returns. The draft is NOT saved and NOT sent:
// an agent must review and edit it, then send it themselves.
public class DraftReplyResponse
{
    public Guid TicketId { get; set; }
    public string Draft { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; // "Groq" or "Mock"
    public bool IsSent { get; set; } // always false: the system never sends AI replies
    public string Notice { get; set; } = "AI-generated draft. Review and edit it before sending. It has not been sent.";
    public DateTime GeneratedAt { get; set; }
}
