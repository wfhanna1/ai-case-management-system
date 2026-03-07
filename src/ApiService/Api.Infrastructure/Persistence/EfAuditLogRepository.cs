using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfAuditLogRepository : IAuditLogRepository
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<EfAuditLogRepository> _logger;

    public EfAuditLogRepository(IntakeDbContext db, ILogger<EfAuditLogRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<Unit>> SaveAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        try
        {
            _db.AuditLogEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit log entry {EntryId}", entry.Id);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<IReadOnlyList<AuditLogEntry>>> ListRecentByTenantAsync(
        TenantId tenantId, int limit, CancellationToken ct = default)
    {
        try
        {
            var entries = await _db.AuditLogEntries
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .ToListAsync(ct);
            return Result<IReadOnlyList<AuditLogEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list recent audit log entries for tenant {TenantId}",
                tenantId.Value);
            return Result<IReadOnlyList<AuditLogEntry>>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<IReadOnlyList<AuditLogEntry>>> ListByDocumentAsync(
        DocumentId documentId, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _db.AuditLogEntries
                .Where(a => a.DocumentId == documentId && a.TenantId == tenantId)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);
            return Result<IReadOnlyList<AuditLogEntry>>.Success(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to list audit log entries for document {DocumentId} tenant {TenantId}",
                documentId.Value, tenantId.Value);
            return Result<IReadOnlyList<AuditLogEntry>>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }
}
