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
        Guid? templateId,
        TenantId tenantId,
        string fileName,
        string storageKey,
        CancellationToken ct = default);

}
