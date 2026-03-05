using SharedKernel;

namespace Api.Domain.Aggregates.Events;

public sealed class DocumentSubmittedEvent : DomainEvent
{
    public DocumentId DocumentId { get; }
    public TenantId TenantId { get; }

    public DocumentSubmittedEvent(DocumentId documentId, TenantId tenantId)
    {
        DocumentId = documentId;
        TenantId = tenantId;
    }
}

public sealed class DocumentProcessingStartedEvent : DomainEvent
{
    public DocumentId DocumentId { get; }
    public TenantId TenantId { get; }

    public DocumentProcessingStartedEvent(DocumentId documentId, TenantId tenantId)
    {
        DocumentId = documentId;
        TenantId = tenantId;
    }
}

public sealed class DocumentCompletedEvent : DomainEvent
{
    public DocumentId DocumentId { get; }
    public TenantId TenantId { get; }

    public DocumentCompletedEvent(DocumentId documentId, TenantId tenantId)
    {
        DocumentId = documentId;
        TenantId = tenantId;
    }
}

public sealed class DocumentFailedEvent : DomainEvent
{
    public DocumentId DocumentId { get; }
    public TenantId TenantId { get; }
    public string Reason { get; }

    public DocumentFailedEvent(DocumentId documentId, TenantId tenantId, string reason)
    {
        DocumentId = documentId;
        TenantId = tenantId;
        Reason = reason;
    }
}
