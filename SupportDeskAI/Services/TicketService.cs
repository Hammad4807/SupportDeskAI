using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Data;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Models;
using SupportDeskAI.Results;

namespace SupportDeskAI.Services;

// Business logic for tickets. Controllers call this; this talks to the database.
public class TicketService : ITicketService
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateTicketRequest> _createValidator;
    private readonly IValidator<UpdateTicketRequest> _updateValidator;
    private readonly IValidator<UpdateTicketStatusRequest> _statusValidator;
    private readonly IValidator<TicketQueryParameters> _queryValidator;

    public TicketService(
        AppDbContext db,
        IValidator<CreateTicketRequest> createValidator,
        IValidator<UpdateTicketRequest> updateValidator,
        IValidator<UpdateTicketStatusRequest> statusValidator,
        IValidator<TicketQueryParameters> queryValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _queryValidator = queryValidator;
    }

    // The LINQ Select used by every query, written once. Expression<...> lets EF Core
    // read the lambda and turn it into a SQL SELECT of only these columns.
    private static readonly Expression<Func<Ticket, TicketResponse>> ToResponse = t => new TicketResponse
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = (int)t.Status,
        Priority = (int)t.Priority,
        Category = t.Category,
        Sentiment = t.Sentiment,
        AiSummary = t.AiSummary,
        SuggestedAction = t.SuggestedAction,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    public async Task<ServiceResult<TicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ServiceResult<TicketResponse>.ValidationFailed(validation.ToDictionary());

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct); // CreatedAt is set automatically in AppDbContext

        return ServiceResult<TicketResponse>.Success((await GetByIdAsync(ticket.Id, ct))!);
    }

    public async Task<ServiceResult<IReadOnlyList<TicketResponse>>> GetAllAsync(TicketQueryParameters query, CancellationToken ct)
    {
        var validation = await _queryValidator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return ServiceResult<IReadOnlyList<TicketResponse>>.ValidationFailed(validation.ToDictionary());

        // Start with all tickets and add a WHERE for each filter that was given.
        IQueryable<Ticket> tickets = _db.Tickets.AsNoTracking();

        if (query.Status.HasValue)
            tickets = tickets.Where(t => t.Status == (TicketStatus)query.Status.Value);

        if (query.Priority.HasValue)
            tickets = tickets.Where(t => t.Priority == (TicketPriority)query.Priority.Value);

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim().ToLower();
            tickets = tickets.Where(t => t.Category != null && t.Category.ToLower() == category);
        }

        var result = await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Select(ToResponse)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<TicketResponse>>.Success(result);
    }

    public async Task<TicketResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<TicketResponse>> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ServiceResult<TicketResponse>.ValidationFailed(validation.ToDictionary());

        // Tracked (no AsNoTracking), so EF Core notices the changes and SaveChanges writes an UPDATE.
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return ServiceResult<TicketResponse>.NotFound(id);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Priority = (TicketPriority)request.Priority;

        await _db.SaveChangesAsync(ct); // UpdatedAt is set automatically in AppDbContext

        return ServiceResult<TicketResponse>.Success((await GetByIdAsync(id, ct))!);
    }

    public async Task<ServiceResult<TicketResponse>> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var validation = await _statusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ServiceResult<TicketResponse>.ValidationFailed(validation.ToDictionary());

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return ServiceResult<TicketResponse>.NotFound(id);

        ticket.Status = (TicketStatus)request.Status;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TicketResponse>.Success((await GetByIdAsync(id, ct))!);
    }
}
