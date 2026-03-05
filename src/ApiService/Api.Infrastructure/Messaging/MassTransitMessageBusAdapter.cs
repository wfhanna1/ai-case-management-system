using Api.Domain.Aggregates;
using Api.Domain.Ports;
using MassTransit;
using Messaging.Contracts.Events;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// Adapts the domain's IMessageBusPort to MassTransit's IPublishEndpoint.
/// Translates domain-level publish calls into typed integration event messages.
/// </summary>
public sealed class MassTransitMessageBusAdapter : IMessageBusPort
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitMessageBusAdapter(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<Unit>> PublishDocumentUploadedAsync(
        DocumentId documentId,
        Guid? templateId,
        TenantId tenantId,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var message = new DocumentUploadedEvent(
                DocumentId: documentId.Value,
                TemplateId: templateId,
                TenantId: tenantId.Value,
                FileName: fileName,
                UploadedAt: DateTimeOffset.UtcNow);

            await _publishEndpoint.Publish(message, ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(new Error(
                "PUBLISH_FAILED",
                $"Failed to publish DocumentUploadedEvent for document {documentId}: {ex.Message}"));
        }
    }

    public async Task<Result<Unit>> PublishEmbeddingRequestedAsync(
        DocumentId documentId,
        TenantId tenantId,
        string textContent,
        Dictionary<string, string> fieldValues,
        CancellationToken ct = default)
    {
        try
        {
            var message = new EmbeddingRequestedEvent(
                DocumentId: documentId.Value,
                TenantId: tenantId.Value,
                TextContent: textContent,
                FieldValues: fieldValues,
                RequestedAt: DateTimeOffset.UtcNow);

            await _publishEndpoint.Publish(message, ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(new Error(
                "PUBLISH_FAILED",
                $"Failed to publish EmbeddingRequestedEvent for document {documentId}: {ex.Message}"));
        }
    }
}
