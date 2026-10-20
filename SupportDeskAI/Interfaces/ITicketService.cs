using SupportDeskAI.DTOs;

namespace SupportDeskAI.Interfaces;

public interface ITicketService
{
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct);
    Task<IReadOnlyList<TicketResponse>> GetAllAsync(CancellationToken ct);
    Task<TicketResponse?> GetByIdAsync(Guid id, CancellationToken ct);
}
