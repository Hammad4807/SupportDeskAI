namespace SupportDeskAI.Constants;

// The only values the AI is allowed to return. Anything else is rejected, never saved.
public static class AiAllowedValues
{
    public static readonly string[] Categories =
    [
        "Account Access",
        "Billing",
        "Technical Issue",
        "Bug Report",
        "Feature Request",
        "General Inquiry",
        "Other"
    ];

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
