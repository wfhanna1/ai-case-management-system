using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        Result<User?> findResult;

        if (request.TenantId.HasValue)
        {
            var tenantId = new TenantId(request.TenantId.Value);
            findResult = await _userRepository.FindByEmailAsync(request.Email, tenantId, ct);
        }
        else
        {
            var countResult = await _userRepository.CountByEmailAsync(request.Email, ct);
            if (countResult.IsFailure)
                return Result<AuthResponse>.Failure(countResult.Error);

            if (countResult.Value > 1)
                return Result<AuthResponse>.Failure(new Error(
                    "TENANT_REQUIRED",
                    "Multiple accounts found for this email. Please specify a tenant."));

            findResult = await _userRepository.FindByEmailOnlyAsync(request.Email, ct);
        }

        if (findResult.IsFailure)
            return Result<AuthResponse>.Failure(findResult.Error);

        if (findResult.Value is null)
            return Result<AuthResponse>.Failure(new Error("INVALID_CREDENTIALS", "Invalid email or password."));

        var user = findResult.Value;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure(new Error("INVALID_CREDENTIALS", "Invalid email or password."));

        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
        user.SetRefreshToken(refreshTokenHash, DateTimeOffset.UtcNow.AddDays(7));

        var updateResult = await _userRepository.UpdateAsync(user, ct);
        if (updateResult.IsFailure)
            return Result<AuthResponse>.Failure(updateResult.Error);

        var accessToken = _tokenService.GenerateAccessToken(user);

        _logger.LogInformation("User {UserId} logged in for tenant {TenantId}", user.Id.Value, user.TenantId.Value);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id.Value, accessToken, refreshToken,
            DateTimeOffset.UtcNow.AddMinutes(_tokenService.AccessTokenExpiryMinutes)));
    }
}
