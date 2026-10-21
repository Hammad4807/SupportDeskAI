using SupportDeskAI.DTOs;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Results;

namespace SupportDeskAI.Services;

// Clearly identified MOCK provider for development and tests when no AI key is available
// (set "Ai:Provider": "Mock" in appsettings.json). It picks values with simple keyword rules,
// never calls the internet, and marks everything it writes with [MOCK].
public class MockAiTicketService : IAiTicketService
{
    public string ProviderName => "Mock";

    public Task<ServiceResult<AiAnalysisResult>> AnalyzeTicketAsync(string title, string description, CancellationToken ct)
    {
        string text = $"{title} {description}".ToLower();

        var (category, priority) =
            ContainsAny(text, "password", "login", "log in", "sign in", "account", "locked") ? ("Account Access", "High") :
            ContainsAny(text, "invoice", "charge", "refund", "payment", "billing", "subscription") ? ("Billing", "Medium") :
            ContainsAny(text, "crash", "error", "bug", "broken", "not working") ? ("Bug Report", "High") :
            ContainsAny(text, "feature", "suggest", "would be nice", "add support") ? ("Feature Request", "Low") :
            ("General Inquiry", "Low");

        string sentiment =
            ContainsAny(text, "angry", "furious", "unacceptable", "worst") ? "Angry" :
            ContainsAny(text, "still", "again", "cannot", "can't", "frustrat") ? "Frustrated" :
            ContainsAny(text, "thanks", "thank you", "great", "love") ? "Positive" :
            "Neutral";

        var analysis = new AiAnalysisResult
        {
            Category = category,
            Priority = priority,
            Sentiment = sentiment,
            Summary = $"[MOCK] {Shorten(title, 200)}",
            SuggestedAction = $"[MOCK] Review the {category.ToLower()} issue and contact the customer."
        };

        return Task.FromResult(ServiceResult<AiAnalysisResult>.Success(analysis));
    }

    public Task<ServiceResult<string>> DraftReplyAsync(string title, string description, string? category, CancellationToken ct)
    {
        string draft =
            "[MOCK DRAFT]\n" +
            "Hello,\n\n" +
            $"Thank you for contacting us about \"{Shorten(title, 100)}\". " +
            "We are sorry for the trouble. Our team is looking into it and will get back to you with the next steps.\n\n" +
            "Kind regards,\nThe Support Team";

        return Task.FromResult(ServiceResult<string>.Success(draft));
    }

    private static bool ContainsAny(string text, params string[] words) => words.Any(text.Contains);

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "...";
}
