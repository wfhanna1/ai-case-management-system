using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Messaging;
using Api.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Messaging.Contracts.Events;
using Messaging.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Messaging.Tests.Consumers;

public sealed class DocumentProcessedConsumerTests
{
    [Fact]
    public async Task DocumentProcessedConsumer_WhenReceived_TransitionsToPendingReview()
    {
        var tenantId = Guid.NewGuid();
        var dbName = $"test-{Guid.NewGuid()}";

        var tenantCtx = new RequestTenantContext();

        await using var provider = new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddSingleton<ITenantContext>(tenantCtx)
            .AddDbContext<IntakeDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName))
            .AddScoped<IDocumentRepository, EfDocumentRepository>()
            .AddScoped<IAuditLogRepository, StubAuditLogRepository>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentProcessedConsumer>();
            })
            .BuildServiceProvider(true);

        // Seed a document in Submitted state. The consumer transitions
        // Submitted -> Processing -> Completed -> PendingReview.
        Guid documentId;
        using (var scope = provider.CreateScope())
        {
            tenantCtx.SetTenant(new TenantId(tenantId));
            var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            var doc = IntakeDocument.Submit(
                new TenantId(tenantId), "test.pdf", "storage/test.pdf");

            documentId = doc.Id.Value;
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentProcessedEvent(
                DocumentId: documentId,
                TenantId: tenantId,
                ExtractedFields: new Dictionary<string, ExtractedFieldResult>
                {
                    ["Name"] = new("Name", "John Doe", 0.95)
                },
                ProcessedAt: DateTimeOffset.UtcNow));

            Assert.True(await harness.Consumed.Any<DocumentProcessedEvent>());
            Assert.False(await harness.Published.Any<Fault<DocumentProcessedEvent>>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            var doc = await db.Documents
                .IgnoreQueryFilters()
                .SingleAsync();

            Assert.Equal(DocumentStatus.PendingReview, doc.Status);
            Assert.NotNull(doc.ProcessedAt);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task DocumentProcessedConsumer_WhenTenantMismatch_SkipsDocument()
    {
        var ownerTenantId = Guid.NewGuid();
        var attackerTenantId = Guid.NewGuid();
        var dbName = $"test-{Guid.NewGuid()}";

        var tenantCtx = new RequestTenantContext();

        await using var provider = new ServiceCollection()
            .AddLogging(b => b.AddProvider(NullLoggerProvider.Instance))
            .AddSingleton<ITenantContext>(tenantCtx)
            .AddDbContext<IntakeDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName))
            .AddScoped<IDocumentRepository, EfDocumentRepository>()
            .AddScoped<IAuditLogRepository, StubAuditLogRepository>()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentProcessedConsumer>();
            })
            .BuildServiceProvider(true);

        Guid documentId;
        using (var scope = provider.CreateScope())
        {
            tenantCtx.SetTenant(new TenantId(ownerTenantId));
            var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            var doc = IntakeDocument.Submit(
                new TenantId(ownerTenantId), "test.pdf", "storage/test.pdf");
            documentId = doc.Id.Value;
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new DocumentProcessedEvent(
                DocumentId: documentId,
                TenantId: attackerTenantId,
                ExtractedFields: new Dictionary<string, ExtractedFieldResult>
                {
                    ["Name"] = new("Name", "John Doe", 0.95)
                },
                ProcessedAt: DateTimeOffset.UtcNow));

            Assert.True(await harness.Consumed.Any<DocumentProcessedEvent>());
            Assert.False(await harness.Published.Any<Fault<DocumentProcessedEvent>>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            var doc = await db.Documents
                .IgnoreQueryFilters()
                .SingleAsync();

            // Document should still be in Submitted state (not updated)
            Assert.Equal(DocumentStatus.Submitted, doc.Status);
            Assert.Null(doc.ProcessedAt);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(
            DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<IReadOnlyList<AuditLogEntry>>.Success(
                (IReadOnlyList<AuditLogEntry>)new List<AuditLogEntry>()));
    }
}
