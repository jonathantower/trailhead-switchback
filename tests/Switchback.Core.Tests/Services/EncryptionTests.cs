using System.Linq;
using System.Security.Cryptography;
using Switchback.Core.Services;

namespace Switchback.Core.Tests.Services;

/// <summary>
/// Unit tests for encryption: round-trip and encrypted output differs from input.
/// Uses an in-memory implementation (same envelope format) so tests don't require Key Vault.
/// </summary>
public class EncryptionTests
{
    /// <summary>In-memory encryption service for testing: uses fixed DEK, no Key Vault.</summary>
    private sealed class InMemoryEncryptionService : IEncryptionService
    {
        private readonly byte[] _dek;

        public InMemoryEncryptionService()
        {
            _dek = new byte[32];
            RandomNumberGenerator.Fill(_dek);
        }

        public Task<byte[]> EncryptAsync(byte[] plaintext, CancellationToken cancellationToken = default)
        {
            byte[] iv = new byte[16];
            RandomNumberGenerator.Fill(iv);
            using var aes = Aes.Create();
            aes.Key = _dek;
            aes.IV = iv;
            byte[] ct = aes.EncryptCbc(plaintext, iv);
            // Format: [4 bytes LE: "wrapped" len = 32][DEK][IV][ciphertext]
            var result = new byte[4 + 32 + 16 + ct.Length];
            result[0] = 32; result[1] = 0; result[2] = 0; result[3] = 0;
            Buffer.BlockCopy(_dek, 0, result, 4, 32);
            Buffer.BlockCopy(iv, 0, result, 36, 16);
            Buffer.BlockCopy(ct, 0, result, 52, ct.Length);
            return Task.FromResult(result);
        }

        public Task<byte[]> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
        {
            int wrappedLen = ciphertext[0] | (ciphertext[1] << 8) | (ciphertext[2] << 16) | (ciphertext[3] << 24);
            var dek = new byte[wrappedLen];
            Buffer.BlockCopy(ciphertext, 4, dek, 0, wrappedLen);
            var iv = new byte[16];
            Buffer.BlockCopy(ciphertext, 4 + wrappedLen, iv, 0, 16);
            int ctLen = ciphertext.Length - 4 - wrappedLen - 16;
            var ct = new byte[ctLen];
            Buffer.BlockCopy(ciphertext, 4 + wrappedLen + 16, ct, 0, ctLen);
            using var aes = Aes.Create();
            aes.Key = dek;
            aes.IV = iv;
            byte[] plain = aes.DecryptCbc(ct, iv);
            return Task.FromResult(plain);
        }
    }

    [Test]
    public async Task Encrypt_decrypt_round_trip_returns_original_plaintext()
    {
        var service = new InMemoryEncryptionService();
        byte[] plaintext = [1, 2, 3, 4, 5];
        byte[] encrypted = await service.EncryptAsync(plaintext);
        byte[] decrypted = await service.DecryptAsync(encrypted);
        await Assert.That(decrypted.SequenceEqual(plaintext)).IsTrue();
    }

    [Test]
    public async Task Encrypt_returns_different_bytes_than_input()
    {
        var service = new InMemoryEncryptionService();
        byte[] plaintext = [1, 2, 3, 4, 5];
        byte[] encrypted = await service.EncryptAsync(plaintext);
        await Assert.That(encrypted.SequenceEqual(plaintext)).IsFalse();
        await Assert.That(encrypted.Length).IsGreaterThan(plaintext.Length);
    }
}
