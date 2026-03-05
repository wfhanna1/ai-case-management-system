using Api.Domain.Ports;

namespace Api.Infrastructure.Auth;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plaintext)
    {
        return BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: 12);
    }

    public bool Verify(string plaintext, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(plaintext, hash);
    }
}
