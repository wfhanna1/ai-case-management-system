using SharedKernel;

namespace Api.Domain.Ports;

/// <summary>
/// Output port for publishing domain events to an async message bus (e.g., RabbitMQ, Azure Service Bus).
/// Decouples the domain from the specific messaging technology.
/// </summary>
public interface IMessageBusPort
{
    /// <summary>
    /// Publishes a domain event to the configured bus. Fire-and-forget from domain perspective.
    /// </summary>
    Task<Result<Unit>> PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : DomainEvent;
}
