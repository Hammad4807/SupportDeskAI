using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Constants;
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

    // FR-06: ask the AI, validate its answer against the allowed values, and only then save it.
    public async Task<ServiceResult<TicketAnalysisResponse>> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return ServiceResult<TicketAnalysisResponse>.NotFound(id);

        // 1. Ask the AI (timeouts / provider errors / unreadable answers come back as errors, not exceptions)
        var aiResult = await _ai.AnalyzeTicketAsync(ticket.Title, ticket.Description, ct);
        if (!aiResult.IsSuccess)
            return ServiceResult<TicketAnalysisResponse>.Failure(aiResult.ErrorType, aiResult.ErrorMessage!);

        // 2. Treat the AI answer as untrusted input: validate every value before using it
        var analysis = aiResult.Value!;
        var validation = await _aiAnalysisValidator.ValidateAsync(analysis, ct);
        if (!validation.IsValid)
        {
            string problems = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Rejected AI analysis for ticket {TicketId}: {Problems}", id, problems);
            return ServiceResult<TicketAnalysisResponse>.Failure(ServiceErrorType.AiInvalidOutput,
                $"The AI suggested values that are not allowed, so nothing was saved. {problems}");
        }

        // 3. Valid: store the official spellings (e.g. "billing" -> "Billing") and save
        ticket.Category = AiAllowedValues.Match(AiAllowedValues.Categories, analysis.Category);
        ticket.Sentiment = AiAllowedValues.Match(AiAllowedValues.Sentiments, analysis.Sentiment);
        ticket.Priority = Enum.Parse<TicketPriority>(analysis.Priority.Trim(), ignoreCase: true);
        ticket.AiSummary = analysis.Summary.Trim();
        ticket.SuggestedAction = analysis.SuggestedAction.Trim();
        await _db.SaveChangesAsync(ct);

        return ServiceResult<TicketAnalysisResponse>.Success(new TicketAnalysisResponse
        {
            TicketId = ticket.Id,
            Category = ticket.Category!,
            Priority = (int)ticket.Priority,
            Sentiment = ticket.Sentiment!,
            Summary = ticket.AiSummary,
            SuggestedAction = ticket.SuggestedAction,
            Provider = _ai.ProviderName
        });
    }

    // FR-07: generate a draft reply for a human to review. It is NOT saved and NOT sent.
    public async Task<ServiceResult<DraftReplyResponse>> DraftReplyAsync(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return ServiceResult<DraftReplyResponse>.NotFound(id);

        var aiResult = await _ai.DraftReplyAsync(ticket.Title, ticket.Description, ticket.Category, ct);
        if (!aiResult.IsSuccess)
            return ServiceResult<DraftReplyResponse>.Failure(aiResult.ErrorType, aiResult.ErrorMessage!);

        string draft = aiResult.Value!.Trim();
        if (draft.Length > AiAllowedValues.DraftMaxLength)
            return ServiceResult<DraftReplyResponse>.Failure(ServiceErrorType.AiInvalidOutput,
                $"The AI draft is longer than {AiAllowedValues.DraftMaxLength} characters.");

        return ServiceResult<DraftReplyResponse>.Success(new DraftReplyResponse
        {
            TicketId = ticket.Id,
            Draft = draft,
            Provider = _ai.ProviderName,
            IsSent = false,
            GeneratedAt = DateTime.UtcNow
        });
    }
}
