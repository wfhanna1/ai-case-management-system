namespace Messaging.Contracts.Events;

/// <summary>
/// Published by ApiService when a new intake document has been successfully stored.
/// Consumed by OcrWorkerService to begin OCR processing.
/// </summary>
public record DocumentUploadedEvent(
    Guid DocumentId,
    Guid? TemplateId,
    Guid TenantId,
    string FileName,
    string StorageKey,
    DateTimeOffset UploadedAt);
