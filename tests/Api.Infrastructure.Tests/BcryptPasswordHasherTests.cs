using Api.Infrastructure.Auth;

namespace Api.Infrastructure.Tests;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = _hasher.Hash("mypassword");

        Assert.True(_hasher.Verify("mypassword", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("mypassword");

        Assert.False(_hasher.Verify("wrongpassword", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentHashesForSameInput()
    {
        var hash1 = _hasher.Hash("mypassword");
        var hash2 = _hasher.Hash("mypassword");

        Assert.NotEqual(hash1, hash2); // BCrypt uses random salt
    }
}
