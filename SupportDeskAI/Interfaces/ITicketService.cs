using SupportDeskAI.DTOs;
using SupportDeskAI.Results;

namespace SupportDeskAI.Interfaces;

// Input is validated inside the service (not in the controller), so every caller
// gets the same rules: the REST controllers today, MCP tools later.
public interface ITicketService
{
    Task<ServiceResult<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<TicketResponse>>> GetAllAsync(TicketQueryParameters query, CancellationToken ct);
    Task<TicketResponse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<TicketResponse>> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken ct);
    Task<ServiceResult<TicketResponse>> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct);
}
