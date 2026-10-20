using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Data;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Models;

namespace SupportDeskAI.Services;

// Business logic for tickets. Controllers call this; this talks to the database.
public class TicketService : ITicketService
{
    private readonly AppDbContext _db;

    public TicketService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct); // CreatedAt is set automatically in AppDbContext

        // Read it back through the same projection so the response shape is always identical.
        return (await GetByIdAsync(ticket.Id, ct))!;
    }

    public async Task<IReadOnlyList<TicketResponse>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Tickets
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = (int)t.Status,
                Priority = (int)t.Priority,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<TicketResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TicketResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = (int)t.Status,
                Priority = (int)t.Priority,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
    }
}
