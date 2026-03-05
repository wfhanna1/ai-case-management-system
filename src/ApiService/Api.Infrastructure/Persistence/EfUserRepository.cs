using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Infrastructure.Persistence;

public sealed class EfUserRepository : IUserRepository
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<EfUserRepository> _logger;

    public EfUserRepository(IntakeDbContext db, ILogger<EfUserRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<User?>> FindByEmailAsync(
        string email, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId, ct);
            return Result<User?>.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find user by email for tenant {TenantId}", tenantId.Value);
            return Result<User?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<User?>> FindByIdAsync(
        UserId id, TenantId tenantId, CancellationToken ct = default)
    {
        try
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);
            return Result<User?>.Success(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find user {UserId} for tenant {TenantId}", id.Value, tenantId.Value);
            return Result<User?>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default)
    {
        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user {UserId}", user.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }

    public async Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default)
    {
        try
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", user.Id.Value);
            return Result<Unit>.Failure(new Error("DB_ERROR", "An internal error occurred"));
        }
    }
}
