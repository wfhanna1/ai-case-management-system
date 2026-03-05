using Api.Domain.Aggregates;

namespace Api.Domain.Ports;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    int AccessTokenExpiryMinutes { get; }
    string GenerateRefreshToken();
    string HashRefreshToken(string rawToken);
}
