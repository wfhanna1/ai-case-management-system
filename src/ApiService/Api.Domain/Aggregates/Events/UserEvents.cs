using SharedKernel;

namespace Api.Domain.Aggregates.Events;

public sealed class UserRegisteredEvent : DomainEvent
{
    public UserId UserId { get; }
    public TenantId TenantId { get; }

    public UserRegisteredEvent(UserId userId, TenantId tenantId)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
