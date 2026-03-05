using Messaging.Contracts.Models;

namespace Messaging.Contracts.Events;

/// <summary>
/// Published by OcrWorkerService when OCR extraction is complete.
/// Consumed by RagService to begin embedding and indexing.
/// </summary>
public record DocumentProcessedEvent(
    Guid DocumentId,
    Guid TenantId,
    Dictionary<string, ExtractedFieldResult> ExtractedFields,
    DateTimeOffset ProcessedAt);
