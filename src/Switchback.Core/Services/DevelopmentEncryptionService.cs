using System.Security.Cryptography;

namespace Switchback.Core.Services;

/// <summary>
/// Local-only encryption using a fixed key. Use only when Key Vault is not available (e.g. local dev).
/// Do not use in production. Tokens encrypted with this are not compatible with KeyVaultEncryptionService.
/// </summary>
public sealed class DevelopmentEncryptionService : IEncryptionService
{
    private const int AesKeySizeBytes = 32;
    private const int AesBlockSizeBytes = 16;
    private static readonly byte[] FixedKey = new byte[AesKeySizeBytes]
    {
        0x53, 0x77, 0x69, 0x74, 0x63, 0x68, 0x62, 0x61,
        0x63, 0x6b, 0x2d, 0x64, 0x65, 0x76, 0x2d, 0x6b,
        0x65, 0x79, 0x2d, 0x6c, 0x6f, 0x63, 0x61, 0x6c,
        0x2d, 0x6f, 0x6e, 0x6c, 0x79, 0x21, 0x21, 0x21
    };

    public Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] iv = new byte[AesBlockSizeBytes];
        RandomNumberGenerator.Fill(iv);
        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = FixedKey;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }
        var result = new byte[iv.Length + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
        return Task.FromResult(result);
    }

    public Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length < AesBlockSizeBytes)
            throw new ArgumentException("Ciphertext too short", nameof(ciphertext));
        byte[] iv = new byte[AesBlockSizeBytes];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, AesBlockSizeBytes);
        byte[] ct = new byte[ciphertext.Length - AesBlockSizeBytes];
        Buffer.BlockCopy(ciphertext, AesBlockSizeBytes, ct, 0, ct.Length);
        using var aes = Aes.Create();
        aes.Key = FixedKey;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ct, 0, ct.Length);
        return Task.FromResult(plaintext);
    }
}
