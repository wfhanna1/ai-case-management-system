using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly IntakeDbContext _db;

    public EfDocumentRepository(IntakeDbContext db)
    {
        _db = db;
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
            return Result<IntakeDocument?>.Failure(new Error("DB_ERROR", ex.Message));
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
            return Result<IReadOnlyList<IntakeDocument>>.Failure(new Error("DB_ERROR", ex.Message));
        }
    }

    public async Task<Result<Unit>> SaveAsync(IntakeDocument document, CancellationToken ct = default)
    {
        try
        {
            var entry = _db.Entry(document);
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                var tracked = _db.ChangeTracker.Entries<IntakeDocument>()
                    .FirstOrDefault(e => e.Entity.Id == document.Id);
                if (tracked is not null)
                    tracked.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

                _db.Documents.Update(document);
            }

            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(new Error("DB_ERROR", ex.Message));
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
            return Result<Unit>.Failure(new Error("DB_ERROR", ex.Message));
        }
    }
}
