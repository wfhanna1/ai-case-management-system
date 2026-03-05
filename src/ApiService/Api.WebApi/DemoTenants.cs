using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.WebApi;

/// <summary>
/// Well-known tenant IDs for local development and demo environments.
/// </summary>
public static class DemoTenants
{
    public static readonly Guid AlphaTenantId = new("a1b2c3d4-0000-0000-0000-000000000001");
    public static readonly Guid BetaTenantId  = new("b2c3d4e5-0000-0000-0000-000000000002");

    public const string DemoPassword = "Demo123!";

    /// <summary>
    /// Seeds demo documents and users if the database is empty. Called at startup in Development.
    /// </summary>
    public static async Task SeedAsync(IntakeDbContext db, IPasswordHasher passwordHasher)
    {
        await SeedDocumentsAsync(db);
        await SeedUsersAsync(db, passwordHasher);
    }

    private static async Task SeedDocumentsAsync(IntakeDbContext db)
    {
        if (await db.Documents.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(AlphaTenantId);
        var beta = new TenantId(BetaTenantId);

        db.Documents.Add(IntakeDocument.Submit(alpha, "alpha-intake-form.pdf", "demo/alpha/alpha-intake-form.pdf"));
        db.Documents.Add(IntakeDocument.Submit(beta, "beta-claim-form.pdf", "demo/beta/beta-claim-form.pdf"));

        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(IntakeDbContext db, IPasswordHasher passwordHasher)
    {
        if (await db.Users.AnyAsync())
            return;

        var hash = passwordHasher.Hash(DemoPassword);
        var alpha = new TenantId(AlphaTenantId);
        var beta = new TenantId(BetaTenantId);

        db.Users.AddRange(
            User.Register(alpha, "admin@alpha.demo", hash, [UserRole.Admin]),
            User.Register(alpha, "worker@alpha.demo", hash, [UserRole.IntakeWorker]),
            User.Register(beta, "admin@beta.demo", hash, [UserRole.Admin]),
            User.Register(beta, "worker@beta.demo", hash, [UserRole.IntakeWorker])
        );

        await db.SaveChangesAsync();
    }
}
