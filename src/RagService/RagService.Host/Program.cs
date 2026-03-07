using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Qdrant.Client;
using RagService.Application;
using RagService.Domain.Ports;
using RagService.Host;
using RagService.Infrastructure.Embeddings;
using RagService.Infrastructure.Messaging;
using RagService.Infrastructure.VectorStore;
using SharedKernel.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Observability -- OpenTelemetry distributed tracing + structured logging
// ---------------------------------------------------------------------------
var serviceDiagnostics = new ServiceDiagnostics("RagService");
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

builder.Logging.AddStructuredConsoleLogging();

// ---------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------
var rabbitHost = builder.Configuration["RabbitMQ__Host"]
    ?? builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ__Username"]
    ?? builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ__Password"]
    ?? builder.Configuration["RabbitMQ:Password"] ?? "guest";
var qdrantHttpPort = builder.Configuration["Qdrant:HttpPort"] ?? "6333";
var qdrantHealthUrl = $"http://{builder.Configuration["Qdrant:Host"] ?? "localhost"}:{qdrantHttpPort}/healthz";

builder.Services.AddHealthChecks()
    .AddRabbitMQ(async _ =>
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = rabbitHost,
            UserName = rabbitUser,
            Password = rabbitPass
        };
        return await factory.CreateConnectionAsync();
    }, name: "rabbitmq", tags: ["messaging"])
    .AddUrlGroup(new Uri(qdrantHealthUrl), name: "qdrant", tags: ["vectordb"]);

// ---------------------------------------------------------------------------
// Messaging -- RabbitMQ via MassTransit (12-factor: config from env)
// ---------------------------------------------------------------------------
builder.Services.AddRagMessaging(builder.Configuration);

// ---------------------------------------------------------------------------
// Embedding + Vector Store
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IEmbeddingPort, MockEmbeddingAdapter>();
builder.Services.AddSingleton<EmbedDocumentHandler>();
builder.Services.AddSingleton<SimilarDocumentsHandler>();
builder.Services.AddSingleton<FindSimilarByTextHandler>();

var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.TryParse(builder.Configuration["Qdrant:Port"], out var p) ? p : 6334;
builder.Services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
builder.Services.AddSingleton<IVectorStorePort, QdrantVectorStoreAdapter>();

// ---------------------------------------------------------------------------
// Hosted services
// ---------------------------------------------------------------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<RagDataSeeder>();
}
builder.Services.AddHostedService<RagWorkerService>();

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

app.MapGet("/api/similar", async (
    Guid documentId,
    Guid tenantId,
    int topK,
    SimilarDocumentsHandler handler,
    CancellationToken ct) =>
{
    if (topK is < 1 or > 50)
        return Results.BadRequest(new { error = "topK must be between 1 and 50." });

    var result = await handler.HandleAsync(documentId, tenantId, topK, ct);
    if (result.IsFailure)
        return Results.Problem(result.Error.Message, statusCode: 500);

    return Results.Ok(new { data = result.Value });
});

app.MapPost("/api/similar-by-text", async (
    SimilarByTextRequest request,
    FindSimilarByTextHandler handler,
    CancellationToken ct) =>
{
    if (request.TopK is < 1 or > 50)
        return Results.BadRequest(new { error = "topK must be between 1 and 50." });

    var result = await handler.HandleAsync(request.Text, request.TenantId, request.TopK, ct);
    if (result.IsFailure)
        return Results.Problem(result.Error.Message, statusCode: 500);

    return Results.Ok(new { data = result.Value });
});

app.Run();
