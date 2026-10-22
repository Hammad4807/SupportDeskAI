using SupportDeskAI.Configuration;
using SupportDeskAI.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database, services, AI provider and validators - shared with the MCP server (SupportDeskAI.Mcp).
ServiceRegistration.AddSupportDeskServices(builder.Services, builder.Configuration);

// CORS: allow the React frontend (different port) to call this API.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// Global exception handling (built-in .NET 8+): unhandled errors go to GlobalExceptionHandler.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---------- Middleware pipeline (order matters!) ----------
// 1. CORS first, so even error responses (500) carry the CORS headers the browser needs
app.UseCors("Frontend");

// 2. Request logging - outermost of our two, so it times the whole request
//    and logs the final status code, including the 500 written by the error handler
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Global exception handling - catches errors from everything below it and calls GlobalExceptionHandler.
//    Our handler already logs the error (with the traceId), so the framework's own error log is suppressed.
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Swagger UI at /swagger, reading the document generated at /openapi/v1.json.
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "SupportDesk AI v1"));
}
else
{
    app.UseHttpsRedirection();
}

// 4. Endpoint execution (controllers)
app.MapControllers();

app.Run();
