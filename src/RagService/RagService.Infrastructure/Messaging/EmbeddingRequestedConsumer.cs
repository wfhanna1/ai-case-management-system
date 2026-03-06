using System.Diagnostics;
using MassTransit;
using Messaging.Contracts.Events;
using Microsoft.Extensions.Logging;
using RagService.Application;

namespace RagService.Infrastructure.Messaging;

/// <summary>
/// Consumes EmbeddingRequestedEvent messages and triggers vector embedding and storage.
/// After processing it publishes EmbeddingCompletedEvent so the pipeline can
/// mark the document as fully indexed.
/// </summary>
public sealed class EmbeddingRequestedConsumer : IConsumer<EmbeddingRequestedEvent>
{
    private readonly EmbedDocumentHandler _handler;
    private readonly ILogger<EmbeddingRequestedConsumer> _logger;

    public EmbeddingRequestedConsumer(
        EmbedDocumentHandler handler,
        ILogger<EmbeddingRequestedConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmbeddingRequestedEvent> context)
    {
        var message = context.Message;

        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = message.TenantId,
            ["DocumentId"] = message.DocumentId,
            ["TraceId"] = Activity.Current?.TraceId.ToString()
        });

        _logger.LogInformation(
            "Received EmbeddingRequestedEvent. DocumentId={DocumentId} TenantId={TenantId} FieldCount={FieldCount}",
            message.DocumentId,
            message.TenantId,
            message.FieldValues.Count);

        var result = await _handler.HandleAsync(
            message.DocumentId,
            message.TenantId,
            message.TextContent,
            message.FieldValues,
            context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError(
                "Embedding failed for DocumentId={DocumentId}: {Error}",
                message.DocumentId, result.Error.Message);
            throw new InvalidOperationException(
                $"Embedding failed: {result.Error.Message}");
        }

        var completed = new EmbeddingCompletedEvent(
            DocumentId: message.DocumentId,
            TenantId: message.TenantId,
            CompletedAt: DateTimeOffset.UtcNow);

        await context.Publish(completed, context.CancellationToken);

        _logger.LogInformation(
            "Published EmbeddingCompletedEvent. DocumentId={DocumentId}",
            message.DocumentId);
    }
}
