using ANPT.Infrastructure.Security;

namespace ANPT.Tests.Security;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_Succeeds()
    {
        var hash = _hasher.HashPassword("TestPassword123!");
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.Contains('.', hash);
    }

    [Fact]
    public void VerifyPassword_SamePassword_Succeeds()
    {
        var password = "CorrectHorseBatteryStaple!";
        var hash = _hasher.HashPassword(password);
        Assert.True(_hasher.VerifyPassword(password, hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_Fails()
    {
        var hash = _hasher.HashPassword("RightPassword!");
        Assert.False(_hasher.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void HashPassword_DifferentSalts_ProduceDifferentHashes()
    {
        var password = "SamePassword!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);
        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword(password, hash1));
        Assert.True(_hasher.VerifyPassword(password, hash2));
    }

    [Fact]
    public void VerifyPassword_EmptyOrInvalidHash_Fails()
    {
        Assert.False(_hasher.VerifyPassword("anything", ""));
        Assert.False(_hasher.VerifyPassword("anything", "not-a-valid-hash"));
        Assert.False(_hasher.VerifyPassword("", "1.10000.aa.bb"));
    }

    [Fact]
    public void HashPassword_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(""));
    }
}
