using Api.Domain.Aggregates;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Infrastructure.Tests;

public sealed class CrossTenantIsolationTests : IDisposable
{
    private readonly IntakeDbContext _db;
    private readonly RequestTenantContext _tenantCtx = new();
    private readonly EfDocumentRepository _repository;

    public CrossTenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<IntakeDbContext>()
            .UseInMemoryDatabase($"isolation-{Guid.NewGuid()}")
            .Options;
        _db = new IntakeDbContext(options, _tenantCtx);
        _repository = new EfDocumentRepository(_db, NullLogger<EfDocumentRepository>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FindById_ReturnsNull_WhenDocumentBelongsToDifferentTenant()
    {
        var ownerTenant = TenantId.New();
        var attackerTenant = TenantId.New();

        _tenantCtx.SetTenant(ownerTenant);
        var doc = IntakeDocument.Submit(ownerTenant, "private.pdf", "key/private");
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(attackerTenant);
        var result = await _repository.FindByIdAsync(doc.Id, attackerTenant);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ListByTenant_ReturnsOnlyOwnDocuments()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        _db.Documents.Add(IntakeDocument.Submit(tenantA, "a1.pdf", "key/a1"));
        _db.Documents.Add(IntakeDocument.Submit(tenantA, "a2.pdf", "key/a2"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);
        _db.Documents.Add(IntakeDocument.Submit(tenantB, "b1.pdf", "key/b1"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantA);
        var resultA = await _repository.ListByTenantAsync(tenantA, 1, 20);
        Assert.True(resultA.IsSuccess);
        Assert.Equal(2, resultA.Value.Count);
        Assert.All(resultA.Value, d => Assert.Equal(tenantA, d.TenantId));

        _tenantCtx.SetTenant(tenantB);
        var resultB = await _repository.ListByTenantAsync(tenantB, 1, 20);
        Assert.True(resultB.IsSuccess);
        Assert.Single(resultB.Value);
        Assert.Equal(tenantB, resultB.Value[0].TenantId);
    }

    [Fact]
    public async Task SaveThenList_DifferentTenant_ReturnsEmpty()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        var doc = IntakeDocument.Submit(tenantA, "doc.pdf", "key/doc");
        await _repository.SaveAsync(doc);
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);
        var result = await _repository.ListByTenantAsync(tenantB, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
