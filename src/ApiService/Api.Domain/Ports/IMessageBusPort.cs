using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Output port for publishing integration events to an async message bus (e.g., RabbitMQ).
/// Decouples the domain from the specific messaging technology.
/// Each method corresponds to a distinct event type so callers have compile-time safety.
/// </summary>
public interface IMessageBusPort
{
    /// <summary>
    /// Publishes the fact that a document was uploaded and is ready for OCR.
    /// </summary>
    Task<Result<Unit>> PublishDocumentUploadedAsync(
        DocumentId documentId,
        Guid templateId,
        TenantId tenantId,
        string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Publishes a request for vector embedding of extracted document text.
    /// </summary>
    Task<Result<Unit>> PublishEmbeddingRequestedAsync(
        DocumentId documentId,
        TenantId tenantId,
        string textContent,
        Dictionary<string, string> fieldValues,
        CancellationToken ct = default);
}
