using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<EfDocumentRepository> _logger;

    public EfDocumentRepository(IntakeDbContext db, ILogger<EfDocumentRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<IntakeDocument?>> FindByIdAsync(
        DocumentId id, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var document = await _db.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
            return Result<IntakeDocument?>.Success(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find document {DocumentId} for tenant {TenantId}", id.Value, tenantId.Value);
            return Result<IntakeDocument?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<IReadOnlyList<IntakeDocument>>> ListByTenantAsync(
        TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var documents = await _db.Documents
                .Where(d => d.TenantId == tenantId)
                .OrderByDescending(d => d.SubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            return Result<IReadOnlyList<IntakeDocument>>.Success(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents for tenant {TenantId}", tenantId.Value);
            return Result<IReadOnlyList<IntakeDocument>>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
    {
        try
        {
            var exists = await _db.Documents
                .AnyAsync(d => d.Id == document.Id, ct);

            if (exists)
            {
                _db.Documents.Update(document);
            }
            else
            {
                _db.Documents.Add(document);
            }

            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document {DocumentId}", document.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> DeleteAsync(DocumentId id, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var document = await _db.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, ct);
            if (document is not null)
            {
                _db.Documents.Remove(document);
                await _db.SaveChangesAsync(ct);
            }
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId} for tenant {TenantId}", id.Value, tenantId.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }
}
