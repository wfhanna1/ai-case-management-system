using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfFormTemplateRepository : IFormTemplateRepository
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<EfFormTemplateRepository> _logger;

    public EfFormTemplateRepository(IntakeDbContext db, ILogger<EfFormTemplateRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<FormTemplate?>> FindByIdAsync(
        FormTemplateId id, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var template = await _db.FormTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            return Result<FormTemplate?>.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find form template {TemplateId} for tenant {TenantId}",
                id.Value, tenantId.Value);
            return Result<FormTemplate?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<IReadOnlyList<FormTemplate>>> ListByTenantAsync(
        TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var templates = await _db.FormTemplates
                .Where(t => t.TenantId == tenantId)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);
            return Result<IReadOnlyList<FormTemplate>>.Success(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list form templates for tenant {TenantId}", tenantId.Value);
            return Result<IReadOnlyList<FormTemplate>>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> SaveAsync(FormTemplate template, CancellationToken ct = default)
    {
        try
        {
            _db.FormTemplates.Add(template);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save form template {TemplateId}", template.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }
}
