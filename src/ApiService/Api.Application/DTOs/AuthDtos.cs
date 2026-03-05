namespace Api.Application.DTOs;

public sealed record RegisterUserRequest(
    Guid TenantId,
    string Email,
    string Password,
    IEnumerable<string> Roles);

public sealed record LoginRequest(
    Guid TenantId,
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    Guid UserId,
    Guid TenantId,
    string RefreshToken);

public sealed record AuthResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
