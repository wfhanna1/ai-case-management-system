namespace Messaging.Contracts.Events;

/// <summary>
/// Published by RagService when vector embeddings have been stored successfully.
/// </summary>
public record EmbeddingCompletedEvent(
    Guid DocumentId,
    Guid TenantId,
    DateTimeOffset CompletedAt);
