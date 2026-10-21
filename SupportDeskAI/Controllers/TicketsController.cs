using Microsoft.AspNetCore.Mvc;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Results;

namespace SupportDeskAI.Controllers;

// Thin controller: receives HTTP requests, calls the service, returns HTTP responses.
// Validation and business rules live in TicketService.
[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    // Dependency injection: ASP.NET Core creates the service (registered in Program.cs) and passes it in.
    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    // FR-01: POST /api/tickets
    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return ErrorResponse(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // FR-02: GET /api/tickets?status=0&priority=2&category=Billing (all filters optional)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> GetAll([FromQuery] TicketQueryParameters query, CancellationToken ct)
    {
        var result = await _ticketService.GetAllAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResponse(result);
    }

    // FR-03: GET /api/tickets/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await _ticketService.GetByIdAsync(id, ct);
        if (ticket is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Ticket not found",
                detail: $"No ticket exists with id {id}.");

        return Ok(ticket);
    }

    // FR-04: PUT /api/tickets/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, UpdateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResponse(result);
    }

    // FR-05: PATCH /api/tickets/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketResponse>> UpdateStatus(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResponse(result);
    }

    // FR-06: POST /api/tickets/{id}/analyze
    [HttpPost("{id:guid}/analyze")]
    public async Task<ActionResult<TicketAnalysisResponse>> Analyze(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.AnalyzeAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResponse(result);
    }

    // FR-07: POST /api/tickets/{id}/draft-reply (returns a draft for human review; nothing is sent)
    [HttpPost("{id:guid}/draft-reply")]
    public async Task<ActionResult<DraftReplyResponse>> DraftReply(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.DraftReplyAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : ErrorResponse(result);
    }

    // Turns a failed ServiceResult into the matching HTTP error (ProblemDetails),
    // so every action reports errors the same way.
    private ActionResult ErrorResponse<T>(ServiceResult<T> result)
    {
        return result.ErrorType switch
        {
            ServiceErrorType.Validation =>
                ValidationProblem(new ValidationProblemDetails(result.ValidationErrors ?? new Dictionary<string, string[]>())),
            ServiceErrorType.NotFound =>
                Problem(statusCode: StatusCodes.Status404NotFound, title: "Ticket not found", detail: result.ErrorMessage),
            ServiceErrorType.AiNotConfigured =>
                Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The AI service is not configured.", detail: result.ErrorMessage),
            ServiceErrorType.AiTimeout =>
                Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "The AI service timed out.", detail: result.ErrorMessage),
            ServiceErrorType.AiProviderError =>
                Problem(statusCode: StatusCodes.Status502BadGateway, title: "The AI service failed.", detail: result.ErrorMessage),
            ServiceErrorType.AiInvalidOutput =>
                Problem(statusCode: StatusCodes.Status502BadGateway, title: "The AI returned an invalid response.", detail: result.ErrorMessage),
            _ =>
                Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong.")
        };
    }
}
