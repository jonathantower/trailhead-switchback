namespace Switchback.Core.Services;

/// <summary>
/// Envelope encryption for sensitive data (e.g. OAuth tokens). Uses a Key Vault key to wrap/unwrap a data encryption key (DEK); data is encrypted with AES-256.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypt plaintext; returns blob containing wrapped DEK + IV + ciphertext. Never log the result.</summary>
    Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default);

    /// <summary>Decrypt blob produced by EncryptAsync.</summary>
    Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default);
}
