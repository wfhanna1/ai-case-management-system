using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class LoginHandlerTests
{
    private readonly StubUserRepository _userRepo = new();
    private readonly StubPasswordHasher _passwordHasher = new();
    private readonly StubTokenService _tokenService = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(
            _userRepo, _passwordHasher, _tokenService,
            NullLogger<LoginHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var tenantId = TenantId.New();
        _userRepo.ExistingUser = User.Register(tenantId, "test@example.com", "hashed-password123", [UserRole.IntakeWorker]);
        var request = new LoginRequest(tenantId.Value, "test@example.com", "password123");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ReturnsFailure()
    {
        var request = new LoginRequest(Guid.NewGuid(), "nonexistent@example.com", "password123");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_ReturnsFailure()
    {
        var tenantId = TenantId.New();
        _userRepo.ExistingUser = User.Register(tenantId, "test@example.com", "hashed-correct", [UserRole.IntakeWorker]);
        var request = new LoginRequest(tenantId.Value, "test@example.com", "wrongpassword");

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
    }

    // --- Test Doubles ---

    private sealed class StubUserRepository : IUserRepository
    {
        public User? ExistingUser { get; set; }

        public Task<Result<User?>> FindByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<User?>> FindByIdAsync(UserId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));

        public Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default)
            => Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string plaintext) => "hashed-" + plaintext;
        public bool Verify(string plaintext, string hash) => hash == "hashed-" + plaintext;
    }

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(User user) => "access-token";
        public string GenerateRefreshToken() => "refresh-token";
        public string HashRefreshToken(string rawToken) => "hashed-" + rawToken;
    }
}
