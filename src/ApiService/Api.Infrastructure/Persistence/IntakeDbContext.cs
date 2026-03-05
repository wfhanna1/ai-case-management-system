using Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class IntakeDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public DbSet<IntakeDocument> Documents => Set<IntakeDocument>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

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

            entity.Property(d => d.ReviewedBy)
                .HasConversion(
                    id => id == null ? (Guid?)null : id.Value,
                    value => value == null ? null : new UserId(value.Value))
                .HasColumnName("reviewed_by");

            entity.Property(d => d.ReviewedAt).HasColumnName("reviewed_at");

            entity.Navigation(d => d.ExtractedFields).HasField("_extractedFields");
            entity.OwnsMany(d => d.ExtractedFields, fields =>
            {
                fields.ToJson("extracted_fields");
                fields.Property(f => f.Name).HasJsonPropertyName("name");
                fields.Property(f => f.Value).HasJsonPropertyName("value");
                fields.Property(f => f.Confidence).HasJsonPropertyName("confidence");
                fields.Property(f => f.CorrectedValue).HasJsonPropertyName("correctedValue");
            });

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

        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.Ignore(t => t.DomainEvents);
            entity.ToTable("form_templates");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => new FormTemplateId(value))
                .HasColumnName("id");

            entity.Property(t => t.TenantId)
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .HasColumnName("tenant_id");

            entity.Property(t => t.Name).HasColumnName("name").HasMaxLength(256);
            entity.Property(t => t.Description).HasColumnName("description").HasMaxLength(2000);
            entity.Property(t => t.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(64);
            entity.Property(t => t.IsActive).HasColumnName("is_active");
            entity.Property(t => t.CreatedAt).HasColumnName("created_at");
            entity.Property(t => t.UpdatedAt).HasColumnName("updated_at");

            entity.Navigation(t => t.Fields).HasField("_fields");
            entity.OwnsMany(t => t.Fields, fields =>
            {
                fields.ToJson("fields");
                fields.Property(f => f.Label).HasJsonPropertyName("label");
                fields.Property(f => f.FieldType).HasJsonPropertyName("fieldType").HasConversion<string>();
                fields.Property(f => f.IsRequired).HasJsonPropertyName("isRequired");
                fields.Property(f => f.Options).HasJsonPropertyName("options");
            });

            entity.HasIndex(t => t.TenantId).HasDatabaseName("ix_form_templates_tenant_id");

            entity.HasQueryFilter(t =>
                _tenantContext != null &&
                _tenantContext.TenantId != null &&
                t.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_log");

            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                .HasColumnName("id");

            entity.Property(a => a.TenantId)
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value))
                .HasColumnName("tenant_id");

            entity.Property(a => a.DocumentId)
                .HasConversion(
                    id => id.Value,
                    value => new DocumentId(value))
                .HasColumnName("document_id");

            entity.Property(a => a.Action)
                .HasColumnName("action")
                .HasConversion<string>()
                .HasMaxLength(64);

            entity.Property(a => a.PerformedBy)
                .HasConversion(
                    id => id == null ? (Guid?)null : id.Value,
                    value => value == null ? null : new UserId(value.Value))
                .HasColumnName("performed_by");

            entity.Property(a => a.Timestamp).HasColumnName("timestamp");
            entity.Property(a => a.FieldName).HasColumnName("field_name").HasMaxLength(256);
            entity.Property(a => a.PreviousValue).HasColumnName("previous_value").HasMaxLength(2000);
            entity.Property(a => a.NewValue).HasColumnName("new_value").HasMaxLength(2000);

            entity.HasIndex(a => new { a.TenantId, a.DocumentId })
                .HasDatabaseName("ix_audit_log_tenant_document");

            entity.HasQueryFilter(a =>
                _tenantContext != null &&
                _tenantContext.TenantId != null &&
                a.TenantId == _tenantContext.TenantId);
        });
    }
}
