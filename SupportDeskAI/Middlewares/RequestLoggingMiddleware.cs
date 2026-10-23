using System.Diagnostics;

namespace SupportDeskAI.Middlewares;

// Logs HTTP Method, Request Path, response Status Code and how long the request took (ms).
// A request the client cancelled (page reload, navigation) gets its own clear message instead.
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Start the timer
        var stopwatch = Stopwatch.StartNew();

        // Pass the request to the next middleware
        await _next(context);

        // Response came back, stop the timer
        stopwatch.Stop();

        // RequestAborted is cancelled when the client closed the connection before we finished
        // (UseExceptionHandler then sets status 499 "Client Closed Request"). Not an error.
        if (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("HTTP {Method} {Path} was cancelled by the client after {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
            return;
        }

        _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds);
    }
}
