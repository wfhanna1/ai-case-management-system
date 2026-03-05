using Api.Domain.Aggregates;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Tests;

public sealed class IntakeDbContextTenantFilterTests : IDisposable
{
    private readonly IntakeDbContext _db;
    private readonly RequestTenantContext _tenantCtx = new();

    public IntakeDbContextTenantFilterTests()
    {
        var options = new DbContextOptionsBuilder<IntakeDbContext>()
            .UseInMemoryDatabase($"tenant-filter-{Guid.NewGuid()}")
            .Options;
        _db = new IntakeDbContext(options, _tenantCtx);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Query_ReturnsTenantOwnedDocumentsOnly()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        // Seed: temporarily set tenant context to allow Add (filter does not affect writes)
        _db.Documents.Add(IntakeDocument.Submit(tenantA, "a.pdf", "key/a"));
        _db.Documents.Add(IntakeDocument.Submit(tenantA, "b.pdf", "key/b"));
        _db.Documents.Add(IntakeDocument.Submit(tenantB, "c.pdf", "key/c"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantA);

        var results = await _db.Documents.ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Equal(tenantA, d.TenantId));
    }

    [Fact]
    public async Task Query_WhenNoTenantSet_ReturnsNoDocuments()
    {
        var tenant = TenantId.New();
        _db.Documents.Add(IntakeDocument.Submit(tenant, "x.pdf", "key/x"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // No SetTenant call -- TenantId remains null

        var results = await _db.Documents.ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Find_CrossTenant_ReturnsNull()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        var doc = IntakeDocument.Submit(tenantA, "secret.pdf", "key/s");
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);

        var found = await _db.Documents.FirstOrDefaultAsync(d => d.Id == doc.Id);

        Assert.Null(found);
    }
}
