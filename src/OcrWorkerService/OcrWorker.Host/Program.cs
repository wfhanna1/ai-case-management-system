using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OcrWorker.Application;
using OcrWorker.Domain.Ports;
using OcrWorker.Host;
using OcrWorker.Infrastructure.Messaging;
using OcrWorker.Infrastructure.Ocr;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using SharedKernel.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Observability -- OpenTelemetry distributed tracing + structured logging
// ---------------------------------------------------------------------------
var serviceDiagnostics = new ServiceDiagnostics("OcrWorkerService");
builder.Services.AddSingleton(serviceDiagnostics);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(serviceDiagnostics.ServiceName)
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
});

// ---------------------------------------------------------------------------
// Health checks (12-factor: expose health for orchestrators)
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var rabbitHost = builder.Configuration["RabbitMQ__Host"]
    ?? builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ__Username"]
    ?? builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ__Password"]
    ?? builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: ["db"])
    .AddRabbitMQ(async _ =>
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = rabbitHost,
            UserName = rabbitUser,
            Password = rabbitPass
        };
        return await factory.CreateConnectionAsync();
    }, name: "rabbitmq", tags: ["messaging"]);

// ---------------------------------------------------------------------------
// OCR -- Mock adapter for development (swap for real adapter in production)
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IOcrPort, MockOcrAdapter>();
builder.Services.AddTransient<ProcessDocumentHandler>();

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddOcrMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<OcrWorkerService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------
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
