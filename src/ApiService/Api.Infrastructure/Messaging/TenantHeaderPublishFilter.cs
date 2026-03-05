using MassTransit;
using SharedKernel;

namespace Api.Infrastructure.Messaging;

/// <summary>
/// MassTransit publish pipeline filter that stamps x-tenant-id on every outgoing message
/// when a tenant context is available. Consumers can read this header without
/// deserializing the payload body.
/// </summary>
public sealed class TenantHeaderPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ITenantContext _tenantContext;

    public TenantHeaderPublishFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (_tenantContext.TenantId is { } tenantId)
            context.Headers.Set("x-tenant-id", tenantId.Value.ToString());

        return next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("tenantHeader");
    }
}
