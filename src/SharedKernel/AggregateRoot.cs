namespace SharedKernel;

/// <summary>
/// Base class for aggregate roots. Extends Entity with the ability to record and dispatch domain events.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>
    /// Read-only view of all domain events raised since last cleared.
    /// </summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Records a domain event to be dispatched when the aggregate is persisted.
    /// </summary>
    protected void RaiseDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all recorded domain events. Call after events have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
