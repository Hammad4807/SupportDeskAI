using SupportDeskAI.DTOs;
using SupportDeskAI.Results;

namespace SupportDeskAI.Interfaces;

// Abstraction over the AI provider (PDF: "Use an IAiTicketService abstraction so the provider
// can be replaced or mocked"). Implementations: GroqAiTicketService (real) and MockAiTicketService.
// They only talk to the AI and parse its answer; validating and saving is TicketService's job.
public interface IAiTicketService
{
    string ProviderName { get; }

    Task<ServiceResult<AiAnalysisResult>> AnalyzeTicketAsync(string title, string description, CancellationToken ct);

    Task<ServiceResult<string>> DraftReplyAsync(string title, string description, string? category, CancellationToken ct);
}
