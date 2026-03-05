using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class UserAggregateTests
{
    [Fact]
    public void Register_WithValidData_CreatesUser()
    {
        var tenantId = TenantId.New();
        var user = User.Register(tenantId, "test@example.com", "hashed", [UserRole.IntakeWorker]);

        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("hashed", user.PasswordHash);
        Assert.Single(user.Roles);
        Assert.Equal(UserRole.IntakeWorker, user.Roles[0]);
        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void Register_WithEmptyEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(TenantId.New(), "", "hashed", [UserRole.IntakeWorker]));
    }

    [Fact]
    public void Register_WithNoRoles_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Register(TenantId.New(), "test@example.com", "hashed", []));
    }

    [Fact]
    public void Register_WithMultipleRoles_SetsAll()
    {
        var user = User.Register(TenantId.New(), "admin@example.com", "hashed",
            [UserRole.IntakeWorker, UserRole.Admin]);

        Assert.Equal(2, user.Roles.Count);
        Assert.Contains(UserRole.IntakeWorker, user.Roles);
        Assert.Contains(UserRole.Admin, user.Roles);
    }

    [Fact]
    public void SetRefreshToken_UpdatesFields()
    {
        var user = User.Register(TenantId.New(), "test@example.com", "hashed", [UserRole.IntakeWorker]);
        var expiry = DateTimeOffset.UtcNow.AddDays(7);

        var result = user.SetRefreshToken("token-hash", expiry);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-hash", user.RefreshTokenHash);
        Assert.Equal(expiry, user.RefreshTokenExpiresAt);
    }

    [Fact]
    public void RevokeRefreshToken_ClearsFields()
    {
        var user = User.Register(TenantId.New(), "test@example.com", "hashed", [UserRole.IntakeWorker]);
        user.SetRefreshToken("token-hash", DateTimeOffset.UtcNow.AddDays(7));

        var result = user.RevokeRefreshToken();

        Assert.True(result.IsSuccess);
        Assert.Null(user.RefreshTokenHash);
        Assert.Null(user.RefreshTokenExpiresAt);
    }
}
