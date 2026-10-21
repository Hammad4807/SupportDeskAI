using SupportDeskAI.DTOs;

namespace SupportDeskAI.Interfaces;

// Abstraction over the AI provider (PDF: "Use an IAiTicketService abstraction so the provider
// can be replaced or mocked"). Implementations: GroqAiTicketService (real) and MockAiTicketService.
// They only talk to the AI and parse its answer; validating and saving is TicketService's job.
// Failures are thrown as built-in exceptions: HttpRequestException (503 not configured / 502 provider error),
// InvalidDataException (502 malformed answer) or TimeoutException (504).
public interface IAiTicketService
{
    string ProviderName { get; }

    Task<AiAnalysisResult> AnalyzeTicketAsync(string title, string description, CancellationToken ct);

    Task<string> DraftReplyAsync(string title, string description, string? category, CancellationToken ct);
}
