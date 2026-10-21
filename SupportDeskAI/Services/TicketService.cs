using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Constants;
using SupportDeskAI.Data;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Models;

namespace SupportDeskAI.Services;

// Business logic for tickets. Controllers call this; this talks to the database.
// Expected failures are thrown as built-in exceptions and turned into HTTP errors by GlobalExceptionHandler.
public class TicketService : ITicketService
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateTicketRequest> _createValidator;
    private readonly IValidator<UpdateTicketRequest> _updateValidator;
    private readonly IValidator<UpdateTicketStatusRequest> _statusValidator;
    private readonly IValidator<TicketQueryParameters> _queryValidator;
    private readonly IValidator<AiAnalysisResult> _aiAnalysisValidator;
    private readonly IAiTicketService _ai;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        AppDbContext db,
        IValidator<CreateTicketRequest> createValidator,
        IValidator<UpdateTicketRequest> updateValidator,
        IValidator<UpdateTicketStatusRequest> statusValidator,
        IValidator<TicketQueryParameters> queryValidator,
        IValidator<AiAnalysisResult> aiAnalysisValidator,
        IAiTicketService ai,
        ILogger<TicketService> logger)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
        _queryValidator = queryValidator;
        _aiAnalysisValidator = aiAnalysisValidator;
        _ai = ai;
        _logger = logger;
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

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        // Throws ValidationException (-> 400) when the request is invalid
        await _createValidator.ValidateAndThrowAsync(request, ct);

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Priority = TicketPriority.Medium
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct); // CreatedAt is set automatically in AppDbContext

        return await GetByIdAsync(ticket.Id, ct);
    }

    public async Task<IReadOnlyList<TicketResponse>> GetAllAsync(TicketQueryParameters query, CancellationToken ct)
    {
        await _queryValidator.ValidateAndThrowAsync(query, ct);

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

        return await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Select(ToResponse)
            .ToListAsync(ct);
    }

    public async Task<TicketResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct);

        return ticket ?? throw TicketNotFound(id);
    }

    public async Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);

        // Tracked (no AsNoTracking), so EF Core notices the changes and SaveChanges writes an UPDATE.
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw TicketNotFound(id);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Priority = (TicketPriority)request.Priority;

        await _db.SaveChangesAsync(ct); // UpdatedAt is set automatically in AppDbContext

        return await GetByIdAsync(id, ct);
    }

    public async Task<TicketResponse> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        await _statusValidator.ValidateAndThrowAsync(request, ct);

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw TicketNotFound(id);

        ticket.Status = (TicketStatus)request.Status;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    // FR-06: ask the AI, validate its answer against the allowed values, and only then save it.
    public async Task<TicketAnalysisResponse> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw TicketNotFound(id);

        // 1. Ask the AI (timeouts / provider errors / unreadable answers are thrown by the AI service)
        var analysis = await _ai.AnalyzeTicketAsync(ticket.Title, ticket.Description, ct);

        // 2. Treat the AI answer as untrusted input: validate every value before using it
        var validation = await _aiAnalysisValidator.ValidateAsync(analysis, ct);
        if (!validation.IsValid)
        {
            string problems = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Rejected AI analysis for ticket {TicketId}: {Problems}", id, problems);
            // InvalidDataException -> 502. Thrown BEFORE SaveChanges, so nothing is saved.
            throw new InvalidDataException($"The AI suggested values that are not allowed, so nothing was saved. {problems}");
        }

        // 3. Valid: store the official spellings (e.g. "billing" -> "Billing") and save
        ticket.Category = AiAllowedValues.Match(AiAllowedValues.Categories, analysis.Category);
        ticket.Sentiment = AiAllowedValues.Match(AiAllowedValues.Sentiments, analysis.Sentiment);
        ticket.Priority = Enum.Parse<TicketPriority>(analysis.Priority.Trim(), ignoreCase: true);
        ticket.AiSummary = analysis.Summary.Trim();
        ticket.SuggestedAction = analysis.SuggestedAction.Trim();
        await _db.SaveChangesAsync(ct);

        return new TicketAnalysisResponse
        {
            TicketId = ticket.Id,
            Category = ticket.Category!,
            Priority = (int)ticket.Priority,
            Sentiment = ticket.Sentiment!,
            Summary = ticket.AiSummary,
            SuggestedAction = ticket.SuggestedAction,
            Provider = _ai.ProviderName
        };
    }

    // FR-07: generate a draft reply for a human to review. It is NOT saved and NOT sent.
    public async Task<DraftReplyResponse> DraftReplyAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw TicketNotFound(id);

        string draft = (await _ai.DraftReplyAsync(ticket.Title, ticket.Description, ticket.Category, ct)).Trim();
        if (draft.Length > AiAllowedValues.DraftMaxLength)
            throw new InvalidDataException($"The AI draft is longer than {AiAllowedValues.DraftMaxLength} characters.");

        return new DraftReplyResponse
        {
            TicketId = ticket.Id,
            Draft = draft,
            Provider = _ai.ProviderName,
            IsSent = false,
            GeneratedAt = DateTime.UtcNow
        };
    }

    // KeyNotFoundException -> 404 in GlobalExceptionHandler
    private static KeyNotFoundException TicketNotFound(Guid id) => new($"No ticket exists with id {id}.");
}
