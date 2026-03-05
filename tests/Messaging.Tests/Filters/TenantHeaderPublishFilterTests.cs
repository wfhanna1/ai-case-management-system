using Api.Infrastructure.Messaging;
using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Messaging.Tests.Filters;

public sealed class TenantHeaderPublishFilterTests
{
    [Fact]
    public async Task Publish_WhenTenantSet_StampsXTenantIdHeader()
    {
        var tenantCtx = new RequestTenantContext();
        var tenantId = TenantId.New();
        tenantCtx.SetTenant(tenantId);

        await using var provider = new ServiceCollection()
            .AddSingleton<ITenantContext>(tenantCtx)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddPublishMessageScheduler();
                cfg.UsingInMemory((ctx, busCfg) =>
                {
                    busCfg.UsePublishFilter(typeof(TenantHeaderPublishFilter<>), ctx);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentUploadedEvent(
                Guid.NewGuid(), Guid.NewGuid(), tenantId.Value, "test.pdf", DateTimeOffset.UtcNow));

            Assert.True(await harness.Published.Any<DocumentUploadedEvent>());

            var published = harness.Published.Select<DocumentUploadedEvent>().First();
            var header = published.Context.Headers.Get<string>("x-tenant-id");

            Assert.Equal(tenantId.Value.ToString(), header);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Publish_WhenNoTenantSet_DoesNotAddHeader()
    {
        var tenantCtx = new RequestTenantContext();
        // No SetTenant call -- TenantId remains null

        await using var provider = new ServiceCollection()
            .AddSingleton<ITenantContext>(tenantCtx)
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddPublishMessageScheduler();
                cfg.UsingInMemory((ctx, busCfg) =>
                {
                    busCfg.UsePublishFilter(typeof(TenantHeaderPublishFilter<>), ctx);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentUploadedEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "test.pdf", DateTimeOffset.UtcNow));

            Assert.True(await harness.Published.Any<DocumentUploadedEvent>());

            var published = harness.Published.Select<DocumentUploadedEvent>().First();
            var header = published.Context.Headers.Get<string>("x-tenant-id");

            Assert.Null(header);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
