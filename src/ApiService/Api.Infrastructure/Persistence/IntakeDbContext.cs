using Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class IntakeDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public DbSet<IntakeDocument> Documents => Set<IntakeDocument>();

    /// <summary>
    /// Used by EF Core tooling (dotnet ef migrations) when no tenant context is available.
    /// </summary>
    public IntakeDbContext(DbContextOptions<IntakeDbContext> options) : base(options) { }

    /// <summary>
    /// Used at runtime by the DI container. ITenantContext is scoped per-request.
    /// </summary>
    public IntakeDbContext(DbContextOptions<IntakeDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.Entity<IntakeDocument>(entity =>
        {
            entity.Ignore(d => d.DomainEvents);
            entity.ToTable("documents");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.Id)
                .HasConversion(
                    id => id.Value,
                    value => new DocumentId(value))
                .HasColumnName("id");

            entity.Property(d => d.TenantId)
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .HasColumnName("tenant_id");

            entity.Property(d => d.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(500);
            entity.Property(d => d.StorageKey).HasColumnName("storage_key").HasMaxLength(1000);
            entity.Property(d => d.Status).HasColumnName("status").HasConversion<string>();
            entity.Property(d => d.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(d => d.ProcessedAt).HasColumnName("processed_at");

            entity.HasIndex(d => d.TenantId).HasDatabaseName("ix_documents_tenant_id");

            // Global query filter: all reads are scoped to the current tenant.
            // When _tenantContext is null (EF tooling) or TenantId is null
            // (no tenant resolved), no rows are returned.
            entity.HasQueryFilter(d =>
                _tenantContext != null &&
                _tenantContext.TenantId != null &&
                d.TenantId == _tenantContext.TenantId);

        });
    }
}
