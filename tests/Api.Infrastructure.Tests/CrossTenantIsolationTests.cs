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
    private readonly EfUserRepository _userRepository;

    public CrossTenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<IntakeDbContext>()
            .UseInMemoryDatabase($"isolation-{Guid.NewGuid()}")
            .Options;
        _db = new IntakeDbContext(options, _tenantCtx);
        _repository = new EfDocumentRepository(_db, NullLogger<EfDocumentRepository>.Instance);
        _userRepository = new EfUserRepository(_db, NullLogger<EfUserRepository>.Instance);
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

    [Fact]
    public async Task FindByEmail_WithWrongTenantId_ReturnsNull()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        var user = User.Register(tenantA, "user@a.com", "hash", [UserRole.IntakeWorker]);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);
        var result = await _userRepository.FindByEmailAsync("user@a.com", tenantB);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task FindByEmailOnly_DoesNotLeakTenantNames_ReturnsSingleUser()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        _db.Users.Add(User.Register(tenantA, "unique@a.com", "hash-a", [UserRole.IntakeWorker]));
        await _db.SaveChangesAsync();

        _tenantCtx.SetTenant(tenantB);
        _db.Users.Add(User.Register(tenantB, "unique@b.com", "hash-b", [UserRole.IntakeWorker]));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = await _userRepository.FindByEmailOnlyAsync("unique@a.com");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(tenantA, result.Value.TenantId);
        Assert.Equal("unique@a.com", result.Value.Email);
    }

    [Fact]
    public async Task CountByEmail_DetectsMultipleTenantsWithSameEmail()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        _db.Users.Add(User.Register(tenantA, "shared@test.com", "hash-a", [UserRole.IntakeWorker]));
        await _db.SaveChangesAsync();

        _tenantCtx.SetTenant(tenantB);
        _db.Users.Add(User.Register(tenantB, "shared@test.com", "hash-b", [UserRole.IntakeWorker]));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var countResult = await _userRepository.CountByEmailAsync("shared@test.com");

        Assert.True(countResult.IsSuccess);
        Assert.Equal(2, countResult.Value);
    }

    [Fact]
    public async Task FindByEmail_CannotAccessUserFromAnotherTenant()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        _db.Users.Add(User.Register(tenantA, "admin@corp.com", "hash", [UserRole.Admin]));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);
        var result = await _userRepository.FindByEmailAsync("admin@corp.com", tenantB);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task FormTemplates_CrossTenant_ReturnsEmpty()
    {
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();

        _tenantCtx.SetTenant(tenantA);
        _db.FormTemplates.Add(FormTemplate.Create(
            tenantA, "Template A", "Desc", TemplateType.ChildWelfare,
            [new TemplateField("Name", FieldType.Text, true, null)]));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _tenantCtx.SetTenant(tenantB);
        var templates = await _db.FormTemplates.ToListAsync();

        Assert.Empty(templates);
    }
}
