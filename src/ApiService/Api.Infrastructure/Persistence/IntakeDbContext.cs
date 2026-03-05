using Api.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class IntakeDbContext : DbContext
{
    public DbSet<IntakeDocument> Documents => Set<IntakeDocument>();

    public IntakeDbContext(DbContextOptions<IntakeDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntakeDocument>(entity =>
        {
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
        });
    }
}
