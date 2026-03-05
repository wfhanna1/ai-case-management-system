using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class RefreshTokenHandlerTests
{
    private readonly StubUserRepository _userRepo = new();
    private readonly StubTokenService _tokenService = new();
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _handler = new RefreshTokenHandler(
            _userRepo, _tokenService,
            NullLogger<RefreshTokenHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ValidToken_ReturnsNewTokenPair()
    {
        var tenantId = TenantId.New();
        var user = User.Register(tenantId, "test@example.com", "hashed", [UserRole.IntakeWorker]);
        user.SetRefreshToken("hashed-valid-refresh", DateTimeOffset.UtcNow.AddDays(7));
        _userRepo.ExistingUser = user;

        var request = new RefreshTokenRequest(user.Id.Value, tenantId.Value, "valid-refresh");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_ExpiredToken_ReturnsFailure()
    {
        var tenantId = TenantId.New();
        var user = User.Register(tenantId, "test@example.com", "hashed", [UserRole.IntakeWorker]);
        user.SetRefreshToken("hashed-valid-refresh", DateTimeOffset.UtcNow.AddDays(-1));
        _userRepo.ExistingUser = user;

        var request = new RefreshTokenRequest(user.Id.Value, tenantId.Value, "valid-refresh");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("TOKEN_EXPIRED", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_ReturnsFailure()
    {
        var tenantId = TenantId.New();
        var user = User.Register(tenantId, "test@example.com", "hashed", [UserRole.IntakeWorker]);
        user.SetRefreshToken("hashed-correct-token", DateTimeOffset.UtcNow.AddDays(7));
        _userRepo.ExistingUser = user;

        var request = new RefreshTokenRequest(user.Id.Value, tenantId.Value, "wrong-token");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TOKEN", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsFailure()
    {
        var request = new RefreshTokenRequest(Guid.NewGuid(), Guid.NewGuid(), "some-token");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_TOKEN", result.Error.Code);
    }

    // --- Test Doubles ---

    private sealed class StubUserRepository : IUserRepository
    {
        public User? ExistingUser { get; set; }

        public Task<Result<User?>> FindByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<User?>> FindByEmailOnlyAsync(string email, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<int>> CountByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(Result<int>.Success(ExistingUser is null ? 0 : 1));

        public Task<Result<User?>> FindByIdAsync(UserId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(User user) => "access-token";
        public int AccessTokenExpiryMinutes => 15;
        public string GenerateRefreshToken() => "new-refresh-token";
        public string HashRefreshToken(string rawToken) => "hashed-" + rawToken;
    }
}
