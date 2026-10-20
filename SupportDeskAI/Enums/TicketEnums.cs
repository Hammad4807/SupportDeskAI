
namespace SupportDeskAI.Enums;

// Enums restrict these fields to a fixed set of values,
// so arbitrary text (from users or the AI) can never be saved here.

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}
