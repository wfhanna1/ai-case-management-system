using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterUserHandler> _logger;

    public RegisterUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<RegisterUserHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RegisterUserRequest request,
        CancellationToken ct = default)
    {
        var tenantId = new TenantId(request.TenantId);

        var existingResult = await _userRepository.FindByEmailAsync(request.Email, tenantId, ct);
        if (existingResult.IsFailure)
            return Result<AuthResponse>.Failure(existingResult.Error);

        if (existingResult.Value is not null)
            return Result<AuthResponse>.Failure(new Error("DUPLICATE_EMAIL", "A user with this email already exists."));

        var roles = request.Roles
            .Select(r => Enum.TryParse<UserRole>(r, ignoreCase: true, out var parsed)
                ? parsed
                : (UserRole?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (roles.Count == 0)
            return Result<AuthResponse>.Failure(new Error("INVALID_ROLES", "At least one valid role is required."));

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Register(tenantId, request.Email, passwordHash, roles);

        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
        user.SetRefreshToken(refreshTokenHash, DateTimeOffset.UtcNow.AddDays(7));

        var saveResult = await _userRepository.SaveAsync(user, ct);
        if (saveResult.IsFailure)
            return Result<AuthResponse>.Failure(saveResult.Error);

        var accessToken = _tokenService.GenerateAccessToken(user);

        _logger.LogInformation("User {UserId} registered for tenant {TenantId}", user.Id.Value, tenantId.Value);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id.Value, accessToken, refreshToken, DateTimeOffset.UtcNow.AddMinutes(15)));
    }
}
