using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class DomainEventTests
{
    private sealed class SampleEvent : DomainEvent
    {
        public string Payload { get; }
        public SampleEvent(string payload) { Payload = payload; }
    }

    [Fact]
    public void DomainEvent_OccurredOn_IsSetOnConstruction()
    {
        var before = DateTimeOffset.UtcNow;
        var evt = new SampleEvent("test");
        var after = DateTimeOffset.UtcNow;

        Assert.True(evt.OccurredOn >= before);
        Assert.True(evt.OccurredOn <= after);
    }

    [Fact]
    public void DomainEvent_EventId_IsUniquePerInstance()
    {
        var a = new SampleEvent("a");
        var b = new SampleEvent("b");

        Assert.NotEqual(a.EventId, b.EventId);
    }

    [Fact]
    public void DomainEvent_OccurredOn_IsUtc()
    {
        var evt = new SampleEvent("test");

        Assert.Equal(DateTimeOffset.UtcNow.Offset, evt.OccurredOn.Offset);
    }
}
