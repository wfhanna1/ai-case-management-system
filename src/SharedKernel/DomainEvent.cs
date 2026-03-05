namespace SharedKernel;

/// <summary>
/// Base class for all domain events. Records when the event occurred and assigns a unique identifier.
/// </summary>
public abstract class DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
