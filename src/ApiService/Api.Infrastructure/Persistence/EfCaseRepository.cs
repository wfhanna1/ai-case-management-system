using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfCaseRepository : ICaseRepository
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<EfCaseRepository> _logger;

    public EfCaseRepository(IntakeDbContext db, ILogger<EfCaseRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<Case?>> FindByIdAsync(
        CaseId id, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var entity = await _db.Cases
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct);
            return Result<Case?>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find case {CaseId} for tenant {TenantId}", id.Value, tenantId.Value);
            return Result<Case?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Case?>> FindBySubjectNameAsync(
        string subjectName, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var entity = await _db.Cases
                .IgnoreQueryFilters()
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.SubjectName == subjectName, ct);
            return Result<Case?>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find case by subject name for tenant {TenantId}", tenantId.Value);
            return Result<Case?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> ListByTenantAsync(
        TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = _db.Cases
                .Where(c => c.TenantId == tenantId);

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .Include(c => c.Documents)
                .OrderByDescending(c => c.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Result<(IReadOnlyList<Case>, int)>.Success((items, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cases for tenant {TenantId}", tenantId.Value);
            return Result<(IReadOnlyList<Case>, int)>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<(IReadOnlyList<Case> Items, int TotalCount)>> SearchAsync(
        TenantId tenantId, string? query, DocumentStatus? status,
        DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var q = _db.Cases
                .Where(c => c.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var escaped = EscapeLikePattern(query);
                q = q.Where(c => EF.Functions.ILike(c.SubjectName, $"%{escaped}%", "\\"));
            }

            if (status.HasValue)
                q = q.Where(c => c.Documents.Any(d => d.Status == status.Value));

            if (from.HasValue)
                q = q.Where(c => c.CreatedAt >= from.Value);

            if (to.HasValue)
                q = q.Where(c => c.CreatedAt <= to.Value);

            var totalCount = await q.CountAsync(ct);

            var items = await q
                .Include(c => c.Documents)
                .OrderByDescending(c => c.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Result<(IReadOnlyList<Case>, int)>.Success((items, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search cases for tenant {TenantId}", tenantId.Value);
            return Result<(IReadOnlyList<Case>, int)>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    private static string EscapeLikePattern(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<Result<Unit>> SaveAsync(Case @case, CancellationToken ct = default)
    {
        try
        {
            _db.Cases.Add(@case);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save case {CaseId}", @case.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> UpdateAsync(Case @case, CancellationToken ct = default)
    {
        try
        {
            _db.Cases.Update(@case);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update case {CaseId}", @case.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }
}
