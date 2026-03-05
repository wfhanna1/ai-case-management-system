using Api.Domain.Aggregates;
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

    /// <summary>
    /// Seeds demo documents if the database is empty. Called at startup in Development.
    /// </summary>
    public static async Task SeedAsync(IntakeDbContext db)
    {
        if (await db.Documents.IgnoreQueryFilters().AnyAsync())
            return;

        var alpha = new TenantId(AlphaTenantId);
        var beta = new TenantId(BetaTenantId);

        db.Documents.Add(IntakeDocument.Submit(alpha, "alpha-intake-form.pdf", "demo/alpha/alpha-intake-form.pdf"));
        db.Documents.Add(IntakeDocument.Submit(beta, "beta-claim-form.pdf", "demo/beta/beta-claim-form.pdf"));

        await db.SaveChangesAsync();
    }
}
