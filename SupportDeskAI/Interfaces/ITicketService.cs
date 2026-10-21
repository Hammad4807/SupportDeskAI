using SupportDeskAI.DTOs;

namespace SupportDeskAI.Interfaces;

// Input is validated inside the service (not in the controller), so every caller
// gets the same rules: the REST controllers today, MCP tools later.
// Expected failures are thrown as built-in exceptions; GlobalExceptionHandler turns them into HTTP errors:
//   invalid input -> ValidationException (400), unknown id -> KeyNotFoundException (404),
//   AI problems   -> HttpRequestException / InvalidDataException / TimeoutException (502 / 503 / 504)
public interface ITicketService
{
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct);
    Task<IReadOnlyList<TicketResponse>> GetAllAsync(TicketQueryParameters query, CancellationToken ct);
    Task<TicketResponse> GetByIdAsync(Guid id, CancellationToken ct);
    Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken ct);
    Task<TicketResponse> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct);

    // AI: analysis is validated, then saved on the ticket. A draft reply is only returned, never saved or sent.
    Task<TicketAnalysisResponse> AnalyzeAsync(Guid id, CancellationToken ct);
    Task<DraftReplyResponse> DraftReplyAsync(Guid id, CancellationToken ct);
}
