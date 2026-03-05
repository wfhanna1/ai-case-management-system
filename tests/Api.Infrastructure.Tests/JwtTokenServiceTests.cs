using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Domain.Aggregates;
using Api.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.Infrastructure.Tests;

public sealed class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly JwtSettings _settings;

    public JwtTokenServiceTests()
    {
        _settings = new JwtSettings
        {
            Secret = "TestSecretKeyThatIsAtLeast32Characters!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenExpiryMinutes = 15
        };
        _service = new JwtTokenService(Options.Create(_settings));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var user = User.Register(TenantId.New(), "test@example.com", "hashed",
            [UserRole.IntakeWorker, UserRole.Reviewer]);

        var token = _service.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(_settings.Issuer, jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == _settings.Audience);
        Assert.Equal(user.Id.Value.ToString(), jwt.Subject);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.TenantId.Value.ToString(), jwt.Claims.First(c => c.Type == "tenant_id").Value);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("IntakeWorker", roles);
        Assert.Contains("Reviewer", roles);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        var token = _service.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(token.Length > 20);
    }

    [Fact]
    public void HashRefreshToken_SameInput_SameOutput()
    {
        var hash1 = _service.HashRefreshToken("my-token");
        var hash2 = _service.HashRefreshToken("my-token");

        Assert.Equal(hash1, hash2); // SHA-256 is deterministic
    }

    [Fact]
    public void HashRefreshToken_DifferentInput_DifferentOutput()
    {
        var hash1 = _service.HashRefreshToken("token-a");
        var hash2 = _service.HashRefreshToken("token-b");

        Assert.NotEqual(hash1, hash2);
    }
}
