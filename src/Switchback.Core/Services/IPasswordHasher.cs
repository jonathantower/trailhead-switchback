namespace Switchback.Core.Services;

/// <summary>
/// Hashes and verifies passwords for user sign-up and login. Never log or store plaintext passwords.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
