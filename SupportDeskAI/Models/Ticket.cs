using SupportDeskAI.Enums;

namespace SupportDeskAI.Models;

// Database entity. EF Core maps this class to the "Tickets" table.
// It is never returned directly from the API; DTOs are used instead.
public class Ticket
{
    // UUID primary key. EF Core generates a new Guid automatically when the ticket is added.
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    // Filled in by AI analysis (only after the AI output is validated), so null until analyzed.
    public string? Category { get; set; }
    public string? Sentiment { get; set; }
    public string? AiSummary { get; set; }
    public string? SuggestedAction { get; set; }

    // Set automatically by AppDbContext.SaveChangesAsync (UTC).
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; } // null until the ticket is modified
}
