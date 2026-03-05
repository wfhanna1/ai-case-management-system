using Api.Application.DTOs;
using Api.Domain.Aggregates;
using Api.Domain.Ports;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Application.Commands;

public sealed class RefreshTokenHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        ILogger<RefreshTokenHandler> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken ct = default)
    {
        var tenantId = new TenantId(request.TenantId);
        var userId = new UserId(request.UserId);

        var findResult = await _userRepository.FindByIdAsync(userId, tenantId, ct);
        if (findResult.IsFailure)
            return Result<AuthResponse>.Failure(findResult.Error);

        if (findResult.Value is null)
            return Result<AuthResponse>.Failure(new Error("INVALID_TOKEN", "Invalid refresh token."));

        var user = findResult.Value;

        if (user.RefreshTokenHash is null || user.RefreshTokenExpiresAt is null)
            return Result<AuthResponse>.Failure(new Error("INVALID_TOKEN", "No active refresh token."));

        if (user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            return Result<AuthResponse>.Failure(new Error("TOKEN_EXPIRED", "Refresh token has expired."));

        var incomingHash = _tokenService.HashRefreshToken(request.RefreshToken);
        if (incomingHash != user.RefreshTokenHash)
            return Result<AuthResponse>.Failure(new Error("INVALID_TOKEN", "Invalid refresh token."));

        // Token rotation: issue new pair
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);
        user.SetRefreshToken(newRefreshTokenHash, DateTimeOffset.UtcNow.AddDays(7));

        var updateResult = await _userRepository.UpdateAsync(user, ct);
        if (updateResult.IsFailure)
            return Result<AuthResponse>.Failure(updateResult.Error);

        var accessToken = _tokenService.GenerateAccessToken(user);

        _logger.LogInformation("Token refreshed for user {UserId}", user.Id.Value);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id.Value, accessToken, newRefreshToken, DateTimeOffset.UtcNow.AddMinutes(15)));
    }
}
