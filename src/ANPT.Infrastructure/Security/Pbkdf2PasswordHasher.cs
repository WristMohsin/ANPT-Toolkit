using System.Security.Cryptography;
using System.Text;
using ANPT.Application.Interfaces;

namespace ANPT.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA256 password hasher with unique random salt per password.
/// Format: {version}.{iterations}.{saltBase64}.{hashBase64}
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Version = 1;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is required.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Pbkdf2(password, salt, Iterations, KeySize);

        return string.Join('.',
            Version.ToString(),
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        var parts = passwordHash.Split('.', 4);
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[0], out var version) || version != Version)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 10_000)
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedHash.Length == 0)
            return false;

        var actualHash = Pbkdf2(password, salt, iterations, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int keySize)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            keySize);
    }
}
