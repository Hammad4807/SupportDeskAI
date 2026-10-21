using Microsoft.AspNetCore.Mvc;

namespace SupportDeskAI.Middlewares;

// Global exception handler: catches any error that was not handled further down the pipeline
// (controllers, services, EF Core), logs it and returns a consistent ProblemDetails response.
// The stack trace is only written to the log, never sent to the client.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass the request to the next middleware
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client closed the request (e.g. the user navigated away). This is not an error
            // and nobody is waiting for a response, so only note it in the log.
            _logger.LogInformation("Request {Method} {Path} was cancelled by the client.",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            // Unique id for this request, so we can find it in the logs
            string traceId = context.TraceIdentifier;

            _logger.LogError(ex, "Error on {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                traceId);

            // Once the response has started it can no longer be changed
            if (context.Response.HasStarted)
                throw;

            // Timeouts (e.g. a slow AI provider) get 504, everything else 500
            var problem = ex is TimeoutException
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
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
