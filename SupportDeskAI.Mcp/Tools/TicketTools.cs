using System.ComponentModel;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using SupportDeskAI.Constants;
using SupportDeskAI.DTOs;
using SupportDeskAI.Enums;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Mcp.DTOs;

namespace SupportDeskAI.Mcp.Tools;

// The MCP tools (FR-12). Each tool only:
//   1. checks/converts the arguments sent by the LLM client (untrusted input, FR-13),
//   2. calls the SAME service the REST endpoints use (no business logic here),
//   3. returns a structured result, or a controlled tool error (McpException) without stack traces.
// [McpServerToolType] lets the SDK find this class; each [McpServerTool] method becomes one tool.
[McpServerToolType]
public class TicketTools
{
    private readonly ITicketService _ticketService;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<TicketTools> _logger;

    // Created per tool call (ScopeRequests = true), with services from dependency injection.
    public TicketTools(ITicketService ticketService, IDashboardService dashboardService, ILogger<TicketTools> logger)
    {
        _ticketService = ticketService;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    // ---------- Read-only tools ----------

    [McpServerTool(Name = "list_tickets", Title = "List tickets", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists support tickets, newest first. All filters are optional and can be combined.")]
    public Task<TicketListToolResult> ListTickets(
        [Description("Only tickets with this status: Open, InProgress, Resolved or Closed.")] string? status = null,
        [Description("Only tickets with this priority: Low, Medium, High or Critical.")] string? priority = null,
        [Description("Only tickets with this category: Account Access, Billing, Technical Issue, Bug Report, Feature Request, General Inquiry or Other.")] string? category = null,
        CancellationToken ct = default)
    {
        return RunAsync("list_tickets", async () =>
        {
            var query = new TicketQueryParameters
            {
                Status = status is null ? null : (int)ParseStatus(status),
                Priority = priority is null ? null : (int)ParsePriority(priority),
                Category = category is null ? null : (int)ParseCategory(category)
            };

            var tickets = await _ticketService.GetAllAsync(query, ct);
            return new TicketListToolResult
            {
                Count = tickets.Count,
                Tickets = tickets.Select(ToToolResult).ToList()
            };
        });
    }

    [McpServerTool(Name = "get_ticket", Title = "Get ticket", ReadOnly = true, UseStructuredContent = true)]
    [Description("Gets one support ticket by its id, including any saved AI analysis.")]
    public Task<TicketToolResult> GetTicket(
        [Description("The ticket id (a GUID), e.g. 3f2b8c1e-4a7d-4c1a-9b2e-0f6d5a1c2b3d.")] string id,
        CancellationToken ct = default)
    {
        return RunAsync("get_ticket", async () =>
            ToToolResult(await _ticketService.GetByIdAsync(ParseId(id), ct)));
    }

    [McpServerTool(Name = "get_dashboard_summary", Title = "Dashboard summary", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns ticket counts: total, per status, high priority (High or Critical) and not yet analyzed by AI.")]
    public Task<DashboardSummaryResponse> GetDashboardSummary(CancellationToken ct = default)
    {
        return RunAsync("get_dashboard_summary", () => _dashboardService.GetSummaryAsync(ct));
    }

    // ---------- Tools that change tickets (no delete tool on purpose, FR-13) ----------

    [McpServerTool(Name = "create_ticket", Title = "Create ticket", ReadOnly = false, Destructive = false, Idempotent = false,
        UseStructuredContent = true)]
    [Description("Creates a new support ticket. It starts with status Open and priority Medium.")]
    public Task<TicketToolResult> CreateTicket(
        [Description("Short summary of the problem, 5 to 200 characters.")] string title,
        [Description("Full description of the problem, 10 to 4000 characters.")] string description,
        CancellationToken ct = default)
    {
        // CreateAsync runs the same CreateTicketRequestValidator as POST /api/tickets
        return RunAsync("create_ticket", async () =>
            ToToolResult(await _ticketService.CreateAsync(
                new CreateTicketRequest { Title = title, Description = description }, ct)));
    }

    [McpServerTool(Name = "update_ticket", Title = "Update ticket", ReadOnly = false, Destructive = false, Idempotent = true,
        UseStructuredContent = true)]
    [Description("Updates a ticket's title, description and priority. All three values are required; " +
                 "to change only the status, use update_ticket_status.")]
    public Task<TicketToolResult> UpdateTicket(
        [Description("The ticket id (a GUID).")] string id,
        [Description("New title, 5 to 200 characters.")] string title,
        [Description("New description, 10 to 4000 characters.")] string description,
        [Description("New priority: Low, Medium, High or Critical.")] string priority,
        CancellationToken ct = default)
    {
        // UpdateAsync runs the same UpdateTicketRequestValidator as PUT /api/tickets/{id}
        return RunAsync("update_ticket", async () =>
            ToToolResult(await _ticketService.UpdateAsync(ParseId(id), new UpdateTicketRequest
            {
                Title = title,
                Description = description,
                Priority = (int)ParsePriority(priority)
            }, ct)));
    }

    [McpServerTool(Name = "update_ticket_status", Title = "Update ticket status", ReadOnly = false, Destructive = false,
        Idempotent = true, UseStructuredContent = true)]
    [Description("Changes a ticket's status, e.g. to close it.")]
    public Task<TicketToolResult> UpdateTicketStatus(
        [Description("The ticket id (a GUID).")] string id,
        [Description("New status: Open, InProgress, Resolved or Closed.")] string status,
        CancellationToken ct = default)
    {
        // UpdateStatusAsync runs the same UpdateTicketStatusRequestValidator as PATCH /api/tickets/{id}/status
        return RunAsync("update_ticket_status", async () =>
            ToToolResult(await _ticketService.UpdateStatusAsync(ParseId(id),
                new UpdateTicketStatusRequest { Status = (int)ParseStatus(status) }, ct)));
    }

    // ---------- AI tools ----------

    [McpServerTool(Name = "analyze_ticket", Title = "Analyze ticket with AI", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Asks the AI to analyze a ticket: category, priority, sentiment, a short summary and a suggested next action. " +
                 "The AI answer is validated against the allowed values before it is saved on the ticket; " +
                 "invalid AI output is rejected and nothing is saved.")]
    public Task<TicketAnalysisToolResult> AnalyzeTicket(
        [Description("The ticket id (a GUID).")] string id,
        CancellationToken ct = default)
    {
        // AnalyzeAsync = same flow as POST /api/tickets/{id}/analyze (AI call, validation, save)
        return RunAsync("analyze_ticket", async () =>
        {
            var analysis = await _ticketService.AnalyzeAsync(ParseId(id), ct);
            return new TicketAnalysisToolResult
            {
                TicketId = analysis.TicketId,
                Category = AiAllowedValues.CategoryName((TicketCategory)analysis.Category),
                Priority = ((TicketPriority)analysis.Priority).ToString(),
                Sentiment = analysis.Sentiment,
                Summary = analysis.Summary,
                SuggestedAction = analysis.SuggestedAction,
                Provider = analysis.Provider
            };
        });
    }

    [McpServerTool(Name = "draft_reply", Title = "Draft a reply with AI", ReadOnly = true, Destructive = false,
        Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Asks the AI to write a DRAFT reply to the customer of a ticket. The draft is only returned for a human " +
                 "to review and edit: it is never saved and never sent to anyone.")]
    public Task<DraftReplyResponse> DraftReply(
        [Description("The ticket id (a GUID).")] string id,
        CancellationToken ct = default)
    {
        // DraftReplyAsync = same flow as POST /api/tickets/{id}/draft-reply (IsSent is always false)
        return RunAsync("draft_reply", () => _ticketService.DraftReplyAsync(ParseId(id), ct));
    }

    // ---------- Error handling ----------

    // Runs a tool and turns the built-in exceptions thrown by the services into controlled
    // tool errors (McpException). The SDK sends the McpException message to the client with
    // IsError = true, so the LLM can read it and correct itself. No stack traces are sent.
    private async Task<T> RunAsync<T>(string toolName, Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (McpException)
        {
            throw; // already a controlled error (e.g. invalid id from ParseId)
        }
        catch (ValidationException ex)
        {
            // Same FluentValidation rules as the API (400 there)
            throw new McpException("Invalid input: " + string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or HttpRequestException or InvalidDataException or TimeoutException)
        {
            // Not found (404), AI provider error / not configured (502/503), invalid AI output (502),
            // AI timeout (504): these messages were written by our services and are safe to show.
            _logger.LogWarning("Tool {Tool} failed: {Message}", toolName, ex.Message);
            throw new McpException(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw; // the client cancelled the call
        }
        catch (Exception ex)
        {
            // Unexpected (500 in the API): full details only in the log (stderr), a generic message for the client
            _logger.LogError(ex, "Tool {Tool} failed unexpectedly", toolName);
            throw new McpException("An unexpected error occurred. Please try again later.");
        }
    }

    // ---------- Argument parsing (untrusted input from the LLM) ----------

    private static Guid ParseId(string id) =>
        Guid.TryParse(id?.Trim(), out var guid)
            ? guid
            : throw new McpException($"'{id}' is not a valid ticket id. Use the id (a GUID) from list_tickets.");

    // Only the exact names are accepted (case-insensitive). Numbers like "7" are rejected.
    private static TicketStatus ParseStatus(string value) =>
        ParseEnumName<TicketStatus>(value, "Status must be Open, InProgress, Resolved or Closed.");

    private static TicketPriority ParsePriority(string value) =>
        ParseEnumName<TicketPriority>(value, "Priority must be Low, Medium, High or Critical.");

    // Accepts the readable names ("Account Access") and the enum names ("AccountAccess").
    private static TicketCategory ParseCategory(string value) =>
        AiAllowedValues.Categories.TryGetValue(value.Trim(), out var category)
            ? category
            : ParseEnumName<TicketCategory>(value,
                $"Category must be one of: {string.Join(", ", AiAllowedValues.Categories.Keys)}.");

    private static T ParseEnumName<T>(string value, string errorMessage) where T : struct, Enum
    {
        string? name = Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null ? Enum.Parse<T>(name) : throw new McpException(errorMessage);
    }

    // ---------- Mapping API DTO -> MCP result (numbers -> names) ----------

    private static TicketToolResult ToToolResult(TicketResponse t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = ((TicketStatus)t.Status).ToString(),
        Priority = ((TicketPriority)t.Priority).ToString(),
        Category = t.Category is null ? null : AiAllowedValues.CategoryName((TicketCategory)t.Category.Value),
        Sentiment = t.Sentiment,
        AiSummary = t.AiSummary,
        SuggestedAction = t.SuggestedAction,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
