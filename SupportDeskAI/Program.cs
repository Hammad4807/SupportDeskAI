using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Data;
using SupportDeskAI.Interfaces;
using SupportDeskAI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database: SQLite file, path comes from appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Dependency injection: one TicketService per HTTP request.
builder.Services.AddScoped<ITicketService, TicketService>();

// Registers every FluentValidation validator in this project (e.g. CreateTicketRequestValidator).
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// CORS: allow the React frontend (different port) to call this API.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

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

app.UseCors("Frontend");

app.MapControllers();

app.Run();
