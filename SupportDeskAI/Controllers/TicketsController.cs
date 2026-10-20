using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SupportDeskAI.DTOs;
using SupportDeskAI.Interfaces;

namespace SupportDeskAI.Controllers;

// Thin controller: receives HTTP requests, calls the service, returns HTTP responses.
[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IValidator<CreateTicketRequest> _createValidator;

    // Dependency injection: ASP.NET Core creates these (registered in Program.cs) and passes them in.
    public TicketsController(ITicketService ticketService, IValidator<CreateTicketRequest> createValidator)
    {
        _ticketService = ticketService;
        _createValidator = createValidator;
    }

    // FR-01: POST /api/tickets
    [HttpPost]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var ticket = await _ticketService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    // FR-02: GET /api/tickets
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await _ticketService.GetAllAsync(ct));
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
}
