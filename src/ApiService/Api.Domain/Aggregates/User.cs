using SharedKernel;

namespace Api.Domain.Aggregates;

public sealed class User : AggregateRoot<UserId>
{
    public TenantId TenantId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public IReadOnlyList<UserRole> Roles { get; private set; }
    public string? RefreshTokenHash { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Required by EF Core for materialization.
    private User() : base(UserId.New())
    {
        TenantId = null!;
        Email = null!;
        PasswordHash = null!;
        Roles = [];
    }

    private User(
        UserId id,
        TenantId tenantId,
        string email,
        string passwordHash,
        IReadOnlyList<UserRole> roles) : base(id)
    {
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        Roles = roles;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static User Register(
        TenantId tenantId,
        string email,
        string passwordHash,
        IEnumerable<UserRole> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var roleList = roles.ToList().AsReadOnly();
        if (roleList.Count == 0)
            throw new ArgumentException("At least one role is required.", nameof(roles));

        var user = new User(UserId.New(), tenantId, email, passwordHash, roleList);
        user.RaiseDomainEvent(new Events.UserRegisteredEvent(user.Id, tenantId));
        return user;
    }

    public Result<Unit> SetRefreshToken(string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        RefreshTokenHash = tokenHash;
        RefreshTokenExpiresAt = expiresAt;
        return Result<Unit>.Success(Unit.Value);
    }

    public Result<Unit> RevokeRefreshToken()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiresAt = null;
        return Result<Unit>.Success(Unit.Value);
    }
}
