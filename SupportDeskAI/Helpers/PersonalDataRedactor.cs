using System.Text.RegularExpressions;

namespace SupportDeskAI.Helpers;

// Removes obvious personal data before ticket text is sent to an external AI provider
// (PDF: do not send personal data to an external AI). It is a safety net, not a guarantee.
public static partial class PersonalDataRedactor
{
    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    // 8+ digits, optionally with spaces, dashes, dots, brackets or a leading + (phone and card numbers)
    [GeneratedRegex(@"\+?\d[\d\s\-().]{6,}\d")]
    private static partial Regex LongNumberRegex();

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = EmailRegex().Replace(text, "[email]");
        text = LongNumberRegex().Replace(text, "[number]");
        return text;
    }
}
