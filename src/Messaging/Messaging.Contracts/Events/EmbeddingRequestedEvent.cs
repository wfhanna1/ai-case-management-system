namespace Messaging.Contracts.Events;

/// <summary>
/// Published by ApiService to request vector embedding of extracted text.
/// Consumed by RagService.
/// </summary>
public record EmbeddingRequestedEvent(
    Guid DocumentId,
    Guid TenantId,
    string TextContent,
    Dictionary<string, string> FieldValues,
    DateTimeOffset RequestedAt);
