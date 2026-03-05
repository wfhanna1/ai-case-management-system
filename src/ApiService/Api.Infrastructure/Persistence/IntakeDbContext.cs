using Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class IntakeDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public DbSet<IntakeDocument> Documents => Set<IntakeDocument>();
    public DbSet<User> Users => Set<User>();

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

        modelBuilder.Entity<User>(entity =>
        {
            entity.Ignore(u => u.DomainEvents);
            entity.ToTable("users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                .HasConversion(
                    id => id.Value,
                    value => new UserId(value))
                .HasColumnName("id");

            entity.Property(u => u.TenantId)
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .HasColumnName("tenant_id");

            entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256);
            entity.Property(u => u.CreatedAt).HasColumnName("created_at");
            entity.Property(u => u.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(256);
            entity.Property(u => u.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");

            // Store roles as comma-separated string
            entity.Property(u => u.Roles)
                .HasColumnName("roles")
                .HasMaxLength(256)
                .HasConversion(
                    roles => string.Join(",", roles),
                    csv => csv.Split(",", StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => Enum.Parse<UserRole>(r))
                        .ToList());

            entity.HasIndex(u => new { u.TenantId, u.Email })
                .IsUnique()
                .HasDatabaseName("ix_users_tenant_email");

            // No global query filter on Users: auth endpoints (login, register, refresh)
            // run without a tenant context. Tenant isolation is enforced by the explicit
            // u.TenantId == tenantId predicate in EfUserRepository queries.
        });
    }
}
