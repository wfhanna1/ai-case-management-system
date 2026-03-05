using Api.Application.Commands;
using Api.Domain.Ports;
using Api.Infrastructure.Messaging;
using Api.Infrastructure.Persistence;
using Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration -- all from environment/config, never hardcoded (12-factor)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Missing required configuration: ConnectionStrings__DefaultConnection");

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddControllers();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Handwritten Intake Document Processor API",
        Version = "v1",
        Description = "REST API for submitting and tracking handwritten intake documents."
    });
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgres"]);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// Persistence -- EF Core + PostgreSQL
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<IntakeDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddTransient<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddTransient<IFileStoragePort>(_ =>
    new LocalFileStorageAdapter(Path.Combine(Directory.GetCurrentDirectory(), "storage")));

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddTransient<SubmitDocumentHandler>();

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddApiMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Application
// ---------------------------------------------------------------------------
var app = builder.Build();

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Intake API v1");
    });
}

app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.Run();

// Exposed for integration testing
public partial class Program { }
