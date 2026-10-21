using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SupportDeskAI.ExceptionHandlers;

// Global exception handler (built-in IExceptionHandler, .NET 8+).
// app.UseExceptionHandler() catches any error that was not handled further down the pipeline
// (controllers, services, EF Core) and calls this method. We log it and return a consistent
// ProblemDetails response. The stack trace is only written to the log, never sent to the client.
// (Requests cancelled by the client are already handled quietly by UseExceptionHandler itself.)
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // Unique id for this request, so we can find it in the logs
        string traceId = context.TraceIdentifier;

        _logger.LogError(exception, "Error on {Method} {Path}. TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            traceId);

        // Timeouts get 504, everything else 500
        var problem = exception is TimeoutException
            ? new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "The request timed out.",
                Detail = "The operation took too long. Please try again."
            }
            : new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong.",
                Detail = "An unexpected error occurred. Please try again later."
            };
        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = traceId;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", ct);

        return true; // handled: the framework stops here
    }
}
