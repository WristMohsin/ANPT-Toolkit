namespace ANPT.Application.Interfaces;

/// <summary>
/// Secure password hashing abstraction. Never stores or returns plaintext passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Creates a salted hash of the password. The returned string contains algorithm parameters, salt, and hash.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a previously generated hash using constant-time comparison.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
