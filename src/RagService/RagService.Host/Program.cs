using Qdrant.Client;
using RagService.Application;
using RagService.Domain.Ports;
using RagService.Host;
using RagService.Infrastructure.Embeddings;
using RagService.Infrastructure.Messaging;
using RagService.Infrastructure.VectorStore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks();

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
// Minimal API endpoints
// ---------------------------------------------------------------------------
app.MapHealthChecks("/health");

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

app.Run();
