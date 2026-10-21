using Microsoft.AspNetCore.Mvc;
using SupportDeskAI.DTOs;
using SupportDeskAI.Interfaces;

namespace SupportDeskAI.Controllers;

// Thin controller: receives HTTP requests, calls the service, returns HTTP responses.
// Validation and business rules live in TicketService. Errors (400, 404, 502, 503, 504)
// are thrown by the service and turned into responses by GlobalExceptionHandler.
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
        var ticket = await _ticketService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    // FR-02: GET /api/tickets?status=0&priority=2&category=Billing (all filters optional)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> GetAll([FromQuery] TicketQueryParameters query, CancellationToken ct)
    {
        return Ok(await _ticketService.GetAllAsync(query, ct));
    }

    // FR-03: GET /api/tickets/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> GetById(Guid id, CancellationToken ct)
    {
        return Ok(await _ticketService.GetByIdAsync(id, ct));
    }

    // FR-04: PUT /api/tickets/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, UpdateTicketRequest request, CancellationToken ct)
    {
        return Ok(await _ticketService.UpdateAsync(id, request, ct));
    }

    // FR-05: PATCH /api/tickets/{id}/status
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketResponse>> UpdateStatus(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        return Ok(await _ticketService.UpdateStatusAsync(id, request, ct));
    }

    // FR-06: POST /api/tickets/{id}/analyze
    [HttpPost("{id:guid}/analyze")]
    public async Task<ActionResult<TicketAnalysisResponse>> Analyze(Guid id, CancellationToken ct)
    {
        return Ok(await _ticketService.AnalyzeAsync(id, ct));
    }

    // FR-07: POST /api/tickets/{id}/draft-reply (returns a draft for human review; nothing is sent)
    [HttpPost("{id:guid}/draft-reply")]
    public async Task<ActionResult<DraftReplyResponse>> DraftReply(Guid id, CancellationToken ct)
    {
        return Ok(await _ticketService.DraftReplyAsync(id, ct));
    }
}
