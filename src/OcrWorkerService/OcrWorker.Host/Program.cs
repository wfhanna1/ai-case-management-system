using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OcrWorker.Application;
using OcrWorker.Domain.Ports;
using OcrWorker.Host;
using OcrWorker.Infrastructure.Messaging;
using OcrWorker.Infrastructure.Ocr;
using OcrWorker.Infrastructure.Storage;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SharedKernel.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Observability -- OpenTelemetry distributed tracing + structured logging
// ---------------------------------------------------------------------------
var serviceDiagnostics = new ServiceDiagnostics("OcrWorkerService");
var appMetrics = new AppMetrics(serviceDiagnostics.ServiceName);
builder.Services.AddSingleton(serviceDiagnostics);
builder.Services.AddSingleton(appMetrics);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(serviceDiagnostics.ServiceName)
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("MassTransit")
        .AddMeter(serviceDiagnostics.ServiceName)
        .AddPrometheusExporter());

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});
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
// OCR -- configurable via Ocr:Mode (mock or tesseract)
// ---------------------------------------------------------------------------
var ocrMode = builder.Configuration["Ocr:Mode"] ?? "mock";
if (string.Equals(ocrMode, "tesseract", StringComparison.OrdinalIgnoreCase))
{
    var tessDataPath = builder.Configuration["Ocr:TessDataPath"] ?? "/usr/share/tesseract-ocr/5/tessdata";
    builder.Services.AddSingleton<IOcrPort>(new TesseractOcrAdapter(tessDataPath));
}
else
{
    builder.Services.AddSingleton<IOcrPort, MockOcrAdapter>();
}
builder.Services.AddTransient<ProcessDocumentHandler>();
builder.Services.AddSingleton<IFileStorageReadPort, LocalFileStorageReadAdapter>();

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
app.UseOpenTelemetryPrometheusScrapingEndpoint();

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
