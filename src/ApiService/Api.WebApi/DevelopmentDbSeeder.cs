using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.WebApi;

/// <summary>
/// Seeds demo data on startup in the Development environment.
/// Registered as an IHostedService so Program.cs stays clean.
/// </summary>
public sealed class DevelopmentDbSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevelopmentDbSeeder> _logger;

    public DevelopmentDbSeeder(IServiceScopeFactory scopeFactory, ILogger<DevelopmentDbSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedDocumentsAsync(db);
        await SeedUsersAsync(db, hasher);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedDocumentsAsync(IntakeDbContext db)
    {
        if (await db.Documents.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        db.Documents.Add(IntakeDocument.Submit(alpha, "alpha-intake-form.pdf", "demo/alpha/alpha-intake-form.pdf"));
        db.Documents.Add(IntakeDocument.Submit(beta, "beta-claim-form.pdf", "demo/beta/beta-claim-form.pdf"));

        await db.SaveChangesAsync();
        _logger.LogInformation("Seeded demo documents for Alpha and Beta tenants");
    }

    private async Task SeedUsersAsync(IntakeDbContext db, IPasswordHasher passwordHasher)
    {
        if (await db.Users.AnyAsync())
            return;

        var hash = passwordHasher.Hash(DemoTenants.DemoPassword);
        var alpha = new TenantId(DemoTenants.AlphaTenantId);
        var beta = new TenantId(DemoTenants.BetaTenantId);

        db.Users.AddRange(
            User.Register(alpha, "admin@alpha.demo", hash, [UserRole.Admin]),
            User.Register(alpha, "worker@alpha.demo", hash, [UserRole.IntakeWorker]),
            User.Register(beta, "admin@beta.demo", hash, [UserRole.Admin]),
            User.Register(beta, "worker@beta.demo", hash, [UserRole.IntakeWorker])
        );

        await db.SaveChangesAsync();
        _logger.LogInformation("Seeded demo users for Alpha and Beta tenants");
    }
}
