using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Api.WebApi;
using Api.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.WebApi.Tests.Controllers;

public sealed class AuthControllerTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();
    private const string TestEmail = "test@example.com";
    private const string TestPassword = "Password123!";
    private const string FakeAccessToken = "fake-access-token";
    private const string FakeRefreshToken = "fake-refresh-token";
    private const string FakeRefreshTokenHash = "hashed-fake-refresh-token";

    // ───────────────────── Register ─────────────────────

    [Fact]
    public async Task Register_success_returns_201_with_AuthResponse()
    {
        var controller = BuildController(userExists: false);

        var request = new RegisterUserRequest(TestTenantId, TestEmail, TestPassword, ["IntakeWorker"]);
        var result = await controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);

        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(FakeAccessToken, response.Data.AccessToken);
        Assert.Equal(FakeRefreshToken, response.Data.RefreshToken);
        Assert.NotEqual(Guid.Empty, response.Data.UserId);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_400_with_error()
    {
        var controller = BuildController(userExists: true);

        var request = new RegisterUserRequest(TestTenantId, TestEmail, TestPassword, ["IntakeWorker"]);
        var result = await controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("DUPLICATE_EMAIL", response.Error.Code);
    }

    // ───────────────────── Login ─────────────────────

    [Fact]
    public async Task Login_success_returns_200_with_AuthResponse()
    {
        var controller = BuildController(userExists: true);

        var request = new LoginRequest(TestTenantId, TestEmail, TestPassword);
        var result = await controller.Login(request, CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(FakeAccessToken, response.Data.AccessToken);
        Assert.Equal(FakeRefreshToken, response.Data.RefreshToken);
    }

    [Fact]
    public async Task Login_invalid_credentials_returns_401()
    {
        var controller = BuildController(userExists: false);

        var request = new LoginRequest(TestTenantId, TestEmail, TestPassword);
        var result = await controller.Login(request, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_CREDENTIALS", response.Error.Code);
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        var controller = BuildController(userExists: true, passwordMatches: false);

        var request = new LoginRequest(TestTenantId, TestEmail, "WrongPassword!");
        var result = await controller.Login(request, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_CREDENTIALS", response.Error.Code);
    }

    // ───────────────────── Refresh ─────────────────────

    [Fact]
    public async Task Refresh_success_returns_200_with_AuthResponse()
    {
        var userRepo = new StubUserRepository();
        var tokenService = new StubTokenService();

        // Create a user with a valid refresh token already set
        var user = User.Register(
            new TenantId(TestTenantId), TestEmail, "hashed-password",
            new[] { UserRole.IntakeWorker });
        user.SetRefreshToken(FakeRefreshTokenHash, DateTimeOffset.UtcNow.AddDays(7));
        userRepo.SeedUser(user);

        var controller = BuildController(userRepo, new StubPasswordHasher(true), tokenService);

        var request = new RefreshTokenRequest(user.Id.Value, TestTenantId, FakeRefreshToken);
        var result = await controller.Refresh(request, CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal(FakeAccessToken, response.Data.AccessToken);
        Assert.Equal(FakeRefreshToken, response.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_invalid_token_returns_401()
    {
        var controller = BuildController(userExists: false);

        var request = new RefreshTokenRequest(Guid.NewGuid(), TestTenantId, "bad-token");
        var result = await controller.Refresh(request, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_TOKEN", response.Error.Code);
    }

    [Fact]
    public async Task Refresh_expired_token_returns_401()
    {
        var userRepo = new StubUserRepository();
        var tokenService = new StubTokenService();

        var user = User.Register(
            new TenantId(TestTenantId), TestEmail, "hashed-password",
            new[] { UserRole.IntakeWorker });
        // Set an expired refresh token
        user.SetRefreshToken(FakeRefreshTokenHash, DateTimeOffset.UtcNow.AddDays(-1));
        userRepo.SeedUser(user);

        var controller = BuildController(userRepo, new StubPasswordHasher(true), tokenService);

        var request = new RefreshTokenRequest(user.Id.Value, TestTenantId, FakeRefreshToken);
        var result = await controller.Refresh(request, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponse<AuthResponse>>(objectResult.Value);
        Assert.NotNull(response.Error);
        Assert.Equal("TOKEN_EXPIRED", response.Error.Code);
    }

    // ───────────────────── Helpers ─────────────────────

    private static AuthController BuildController(bool userExists, bool passwordMatches = true)
    {
        var userRepo = new StubUserRepository();
        var passwordHasher = new StubPasswordHasher(passwordMatches);
        var tokenService = new StubTokenService();

        if (userExists)
        {
            var user = User.Register(
                new TenantId(TestTenantId), TestEmail, passwordHasher.Hash(TestPassword),
                new[] { UserRole.IntakeWorker });
            user.SetRefreshToken(FakeRefreshTokenHash, DateTimeOffset.UtcNow.AddDays(7));
            userRepo.SeedUser(user);
        }

        return BuildController(userRepo, passwordHasher, tokenService);
    }

    private static AuthController BuildController(
        IUserRepository userRepo,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        var registerHandler = new RegisterUserHandler(
            userRepo, passwordHasher, tokenService,
            NullLogger<RegisterUserHandler>.Instance);

        var loginHandler = new LoginHandler(
            userRepo, passwordHasher, tokenService,
            NullLogger<LoginHandler>.Instance);

        var refreshHandler = new RefreshTokenHandler(
            userRepo, tokenService,
            NullLogger<RefreshTokenHandler>.Instance);

        return new AuthController(registerHandler, loginHandler, refreshHandler);
    }

    // ───────────────────── Stubs ─────────────────────

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public void SeedUser(User user) => _users.Add(user);

        public Task<Result<User?>> FindByEmailAsync(string email, TenantId tenantId, CancellationToken ct = default)
        {
            var user = _users.FirstOrDefault(u => u.Email == email && u.TenantId == tenantId);
            return Task.FromResult(Result<User?>.Success(user));
        }

        public Task<Result<User?>> FindByEmailOnlyAsync(string email, CancellationToken ct = default)
        {
            var user = _users.FirstOrDefault(u => u.Email == email);
            return Task.FromResult(Result<User?>.Success(user));
        }

        public Task<Result<int>> CountByEmailAsync(string email, CancellationToken ct = default)
        {
            var count = _users.Count(u => u.Email == email);
            return Task.FromResult(Result<int>.Success(count));
        }

        public Task<Result<User?>> FindByIdAsync(UserId id, TenantId tenantId, CancellationToken ct = default)
        {
            var user = _users.FirstOrDefault(u => u.Id == id && u.TenantId == tenantId);
            return Task.FromResult(Result<User?>.Success(user));
        }

        public Task<Result<Unit>> SaveAsync(User user, CancellationToken ct = default)
        {
            _users.Add(user);
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }

        public Task<Result<Unit>> UpdateAsync(User user, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        private readonly bool _verifyReturns;

        public StubPasswordHasher(bool verifyReturns) => _verifyReturns = verifyReturns;

        public string Hash(string plaintext) => $"hashed-{plaintext}";
        public bool Verify(string plaintext, string hash) => _verifyReturns;
    }

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateAccessToken(User user) => FakeAccessToken;
        public int AccessTokenExpiryMinutes => 15;
        public string GenerateRefreshToken() => FakeRefreshToken;
        public string HashRefreshToken(string rawToken) => FakeRefreshTokenHash;
    }
}
