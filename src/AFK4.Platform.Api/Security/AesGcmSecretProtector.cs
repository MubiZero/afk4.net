using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Security;

// AES-256-GCM envelope. Format: "v1.<base64 nonce>.<base64 ciphertext>.<base64 tag>".
// The version prefix lets the key be rotated later without breaking stored values.
public sealed class AesGcmSecretProtector : ISecretProtector, IDisposable
{
    private const string Version = "v1";
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // 128-bit auth tag

    private readonly byte[] key;

    // This is a process-wide Singleton, and it is on the hot path for both TOTP secrets (every
    // 2FA setup/reset) and payment credentials (EskhataMerchantClientFactory decrypts on every
    // payment). AesGcm's OpenSSL-backed EVP context is not safe for concurrent Encrypt/Decrypt on
    // one instance, but serializing every call through a single shared instance+lock would queue
    // the payment path behind 2FA traffic (and vice versa) for the lifetime of the process. A
    // fresh AesGcm per call sidesteps that: it holds no state between calls, and constructing one
    // (an AES key schedule) costs low-single-digit microseconds — negligible next to a full
    // Encrypt/Decrypt call, and nowhere near what a global lock would cost under concurrent load.
    public AesGcmSecretProtector(IOptions<SecretProtectionOptions> options)
    {
        var keyBase64 = options.Value.EncryptionKeyBase64;
        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            throw new InvalidOperationException(
                "Secrets:EncryptionKeyBase64 is not configured; secret protection is unavailable.");
        }

        key = Convert.FromBase64String(keyBase64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Secrets:EncryptionKeyBase64 must decode to 32 bytes, got {key.Length}.");
        }
    }

    public string Protect(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        return string.Join('.',
            Version,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    public string Unprotect(string protectedValue)
    {
        var parts = protectedValue.Split('.');
        if (parts.Length != 4 || parts[0] != Version)
        {
            throw new FormatException("Unrecognized protected-secret envelope.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var ciphertext = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);

        if (nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new FormatException("Unrecognized protected-secret envelope.");
        }

        var plaintextBytes = new byte[ciphertext.Length];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes); // throws CryptographicException on tamper/wrong key
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
    }
}
