using Api.Domain.Aggregates;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Infrastructure.Tests;

public sealed class EfDocumentRepositoryStatsTests : IDisposable
{
    private readonly IntakeDbContext _db;
    private readonly RequestTenantContext _tenantCtx = new();
    private readonly EfDocumentRepository _repository;
    private readonly TenantId _tenantId = TenantId.New();

    public EfDocumentRepositoryStatsTests()
    {
        var options = new DbContextOptionsBuilder<IntakeDbContext>()
            .UseInMemoryDatabase($"stats-{Guid.NewGuid()}")
            .Options;
        _db = new IntakeDbContext(options, _tenantCtx);
        _repository = new EfDocumentRepository(_db, NullLogger<EfDocumentRepository>.Instance);
        _tenantCtx.SetTenant(_tenantId);
    }

    public void Dispose() => _db.Dispose();

    private static IntakeDocument CreatePendingReviewDoc(TenantId tenantId, string name)
    {
        var doc = IntakeDocument.Submit(tenantId, name, $"key/{name}");
        doc.MarkProcessing();
        doc.MarkCompleted();
        doc.MarkPendingReview();
        return doc;
    }

    [Fact]
    public async Task GetStatsAsync_returns_zero_counts_when_no_documents()
    {
        var result = await _repository.GetStatsAsync(_tenantId);

        Assert.True(result.IsSuccess);
        var (pending, processed, avg) = result.Value;
        Assert.Equal(0, pending);
        Assert.Equal(0, processed);
        Assert.Equal(TimeSpan.Zero, avg);
    }

    [Fact]
    public async Task GetStatsAsync_counts_pending_review_documents()
    {
        var doc1 = CreatePendingReviewDoc(_tenantId, "a.pdf");
        var doc2 = CreatePendingReviewDoc(_tenantId, "b.pdf");
        var doc3 = IntakeDocument.Submit(_tenantId, "c.pdf", "key/c"); // stays Submitted

        _db.Documents.AddRange(doc1, doc2, doc3);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await _repository.GetStatsAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PendingReview);
    }

    [Fact]
    public async Task GetStatsAsync_isolates_by_tenant()
    {
        var otherTenant = TenantId.New();

        var myDoc = CreatePendingReviewDoc(_tenantId, "mine.pdf");
        var otherDoc = CreatePendingReviewDoc(otherTenant, "other.pdf");

        _db.Documents.AddRange(myDoc, otherDoc);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await _repository.GetStatsAsync(_tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PendingReview);
    }
}
