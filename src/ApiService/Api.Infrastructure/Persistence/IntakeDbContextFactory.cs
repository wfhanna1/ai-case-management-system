using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations</c> when no
/// running host is available to resolve DI services.
/// </summary>
public sealed class IntakeDbContextFactory : IDesignTimeDbContextFactory<IntakeDbContext>
{
    public IntakeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntakeDbContext>();
        // Connection string is only used to generate migration SQL;
        // it does not need to point at a real database.
        optionsBuilder.UseNpgsql("Host=localhost;Database=intake_processor;Username=postgres;Password=dev");

        return new IntakeDbContext(optionsBuilder.Options);
    }
}
