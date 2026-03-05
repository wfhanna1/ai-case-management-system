using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Ports;

public interface IUserRepository
{
    Task<Result<User?>> FindByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default);
    Task<Result<User?>> FindByEmailOnlyAsync(string email, CancellationToken ct = default);
    Task<Result<int>> CountByEmailAsync(string email, CancellationToken ct = default);
    Task<Result<User?>> FindByIdAsync(UserId id, TenantId tenantId, CancellationToken ct = default);
    Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default);
    Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default);
}
