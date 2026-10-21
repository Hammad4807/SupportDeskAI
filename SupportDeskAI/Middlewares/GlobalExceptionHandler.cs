using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SupportDeskAI.Middlewares;

// Global exception handler (built-in IExceptionHandler, .NET 8+). app.UseExceptionHandler() calls it
// for every exception that is not caught further down (controllers, services, EF Core).
// This is the ONE place where error status codes are chosen, using built-in exception types:
//   ValidationException (FluentValidation) -> 400 with field errors
//   KeyNotFoundException                   -> 404 (ticket not found)
//   HttpRequestException                   -> its StatusCode: 503 (AI not configured) or 502 (AI provider error)
//   InvalidDataException                   -> 502 (AI answer malformed / not allowed)
//   TimeoutException                       -> 504 (AI too slow)
//   anything else                          -> 500 with a generic message
// Stack traces are only written to the log, never sent to the client.
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

        ProblemDetails problem = exception switch
        {
            // Same shape as ASP.NET Core's own 400: { "errors": { "Title": ["Title is required."] } }
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },

            KeyNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Ticket not found",
                Detail = exception.Message
            },

            HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable } => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The AI service is not configured.",
                Detail = exception.Message
            },

            HttpRequestException => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "The AI service failed.",
                Detail = exception.Message
            },

            InvalidDataException => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "The AI returned an invalid response.",
                Detail = exception.Message
            },

            TimeoutException => new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "The request timed out.",
                Detail = exception.Message
            },

            // Unexpected: never show the real message, it may contain internal details
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong.",
                Detail = "An unexpected error occurred. Please try again later."
            }
        };

        // Expected errors (4xx, AI problems) get a short warning; real crashes get the full stack trace.
        if (problem.Status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Error on {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, traceId);
        else
            _logger.LogWarning("{Status} {Title} on {Method} {Path}: {Detail}. TraceId: {TraceId}",
                problem.Status, problem.Title, context.Request.Method, context.Request.Path, problem.Detail, traceId);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = traceId;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        // <object> so a ValidationProblemDetails is written with its "errors" field
        await context.Response.WriteAsJsonAsync<object>(problem, options: null, contentType: "application/problem+json", ct);

        return true; // handled: the framework stops here
    }
}
