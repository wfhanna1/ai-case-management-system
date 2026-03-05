using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.Application.Tests;

public sealed class RegisterUserHandlerTests
{
    private readonly StubUserRepository _userRepo = new();
    private readonly StubPasswordHasher _passwordHasher = new();
    private readonly StubTokenService _tokenService = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(
            _userRepo, _passwordHasher, _tokenService,
            NullLogger<RegisterUserHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsAuthResponse()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "test@example.com", "password123", ["IntakeWorker"]);

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.True(_userRepo.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_DuplicateEmail_ReturnsFailure()
    {
        _userRepo.ExistingUser = User.Register(TenantId.New(), "test@example.com", "hashed", [UserRole.IntakeWorker]);
        var request = new RegisterUserRequest(Guid.NewGuid(), "test@example.com", "password123", ["IntakeWorker"]);

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DUPLICATE_EMAIL", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_InvalidRoles_ReturnsFailure()
    {
        var request = new RegisterUserRequest(Guid.NewGuid(), "test@example.com", "password123", ["NonExistentRole"]);

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_ROLES", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_ReturnsFailure()
    {
        _userRepo.SaveResult = Result<Unit>.Failure(new Error("DB_ERROR", "connection lost"));
        var request = new RegisterUserRequest(Guid.NewGuid(), "test@example.com", "password123", ["IntakeWorker"]);

        var result = await _handler.HandleAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DB_ERROR", result.Error.Code);
    }

    // --- Test Doubles ---

    private sealed class StubUserRepository : IUserRepository
    {
        public User? ExistingUser { get; set; }
        public Result<Unit> SaveResult { get; set; } = Result<Unit>.Success(Unit.Value);
        public bool SaveCalled { get; private set; }

        public Task<Result<User?>> FindByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<User?>> FindByIdAsync(UserId id, TenantId tenantId, CancellationToken ct = default)
            => Task.FromResult(Result<User?>.Success(ExistingUser));

        public Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default)
        {
            SaveCalled = true;
            return Task.FromResult(SaveResult);
        }

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
