using System.Security.Cryptography;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Switchback.Core.Services;

/// <summary>
/// Envelope encryption using Azure Key Vault: DEK is wrapped with Key Vault RSA key; payload encrypted with AES-256-CBC.
/// Key material (CryptographyClient) is cached per instance to avoid repeated Key Vault calls.
/// </summary>
public sealed class KeyVaultEncryptionService : IEncryptionService
{
    private const int AesKeySizeBytes = 32;
    private const int AesBlockSizeBytes = 16;
    private readonly CryptographyClient _cryptoClient;

    public KeyVaultEncryptionService(CryptographyClient cryptoClient)
    {
        _cryptoClient = cryptoClient ?? throw new ArgumentNullException(nameof(cryptoClient));
    }

    public async Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        byte[] dek = new byte[AesKeySizeBytes];
        byte[] iv = new byte[AesBlockSizeBytes];
        RandomNumberGenerator.Fill(dek);
        RandomNumberGenerator.Fill(iv);

        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = dek;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        KeyWrapAlgorithm algorithm = KeyWrapAlgorithm.RsaOaep256;
        var wrapResult = await _cryptoClient.WrapKeyAsync(algorithm, dek, cancellationToken).ConfigureAwait(false);

        // Format: [4 bytes LE: wrapped key length][wrapped key][16 bytes IV][ciphertext]
        int wrappedLen = wrapResult.EncryptedKey.Length;
        var result = new byte[4 + wrappedLen + AesBlockSizeBytes + ciphertext.Length];
        result[0] = (byte)(wrappedLen);
        result[1] = (byte)(wrappedLen >> 8);
        result[2] = (byte)(wrappedLen >> 16);
        result[3] = (byte)(wrappedLen >> 24);
        Buffer.BlockCopy(wrapResult.EncryptedKey, 0, result, 4, wrappedLen);
        Buffer.BlockCopy(iv, 0, result, 4 + wrappedLen, AesBlockSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, 4 + wrappedLen + AesBlockSizeBytes, ciphertext.Length);
        return result;
    }

    public async Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length < 4 + AesBlockSizeBytes + 1)
            throw new ArgumentException("Ciphertext too short.", nameof(ciphertext));

        int wrappedLen = ciphertext[0] | (ciphertext[1] << 8) | (ciphertext[2] << 16) | (ciphertext[3] << 24);
        if (ciphertext.Length < 4 + wrappedLen + AesBlockSizeBytes)
            throw new ArgumentException("Invalid ciphertext format.", nameof(ciphertext));

        var wrappedKey = new byte[wrappedLen];
        Buffer.BlockCopy(ciphertext, 4, wrappedKey, 0, wrappedLen);
        var iv = new byte[AesBlockSizeBytes];
        Buffer.BlockCopy(ciphertext, 4 + wrappedLen, iv, 0, AesBlockSizeBytes);
        int ctLen = ciphertext.Length - 4 - wrappedLen - AesBlockSizeBytes;
        var ct = new byte[ctLen];
        Buffer.BlockCopy(ciphertext, 4 + wrappedLen + AesBlockSizeBytes, ct, 0, ctLen);

        var unwrapResult = await _cryptoClient.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrappedKey, cancellationToken).ConfigureAwait(false);
        byte[] dek = unwrapResult.Key;

        using (var aes = Aes.Create())
        {
            aes.Key = dek;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ct, 0, ct.Length);
        }
    }
}
