using SupportDeskAI.Enums;

namespace SupportDeskAI.Constants;

// The only values the AI is allowed to return. Anything else is rejected, never saved.
public static class AiAllowedValues
{
    // The AI answers with these readable names; each one maps to the TicketCategory saved in the database.
    // Case-insensitive, so "billing" also matches "Billing".
    public static readonly IReadOnlyDictionary<string, TicketCategory> Categories =
        new Dictionary<string, TicketCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["Account Access"] = TicketCategory.AccountAccess,
            ["Billing"] = TicketCategory.Billing,
            ["Technical Issue"] = TicketCategory.TechnicalIssue,
            ["Bug Report"] = TicketCategory.BugReport,
            ["Feature Request"] = TicketCategory.FeatureRequest,
            ["General Inquiry"] = TicketCategory.GeneralInquiry,
            ["Other"] = TicketCategory.Other
        };

    // TicketCategory.BugReport -> "Bug Report" (used when sending the category back to the AI)
    public static string CategoryName(TicketCategory category) =>
        Categories.First(pair => pair.Value == category).Key;

    public static readonly string[] Sentiments =
    [
        "Positive",
        "Neutral",
        "Negative",
        "Frustrated",
        "Angry"
    ];

    public const int SummaryMaxLength = 500;
    public const int SuggestedActionMaxLength = 500;
    public const int DraftMaxLength = 2000;

    // Returns the official spelling ("billing" -> "Billing"), or null if the value is not allowed.
    public static string? Match(string[] allowed, string? value) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
