using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class AggregateRootTests
{
    private sealed class OrderCreatedEvent : DomainEvent
    {
        public string OrderId { get; }
        public OrderCreatedEvent(string orderId) { OrderId = orderId; }
    }

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id) : base(id) { }

        public void Create(string orderId)
        {
            RaiseDomainEvent(new OrderCreatedEvent(orderId));
        }
    }

    [Fact]
    public void AggregateRoot_InitiallyHasNoDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void AggregateRoot_RaisedEvent_AppearsInDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.Create("order-1");

        Assert.Single(aggregate.DomainEvents);
        var evt = Assert.IsType<OrderCreatedEvent>(aggregate.DomainEvents[0]);
        Assert.Equal("order-1", evt.OrderId);
    }

    [Fact]
    public void AggregateRoot_ClearDomainEvents_RemovesAllEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.Create("order-1");
        aggregate.Create("order-2");

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void AggregateRoot_MultipleEvents_AllRecorded()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.Create("order-1");
        aggregate.Create("order-2");

        Assert.Equal(2, aggregate.DomainEvents.Count);
    }
}
