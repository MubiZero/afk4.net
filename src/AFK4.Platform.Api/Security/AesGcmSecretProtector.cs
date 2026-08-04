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
    private readonly AesGcm aes;
    private readonly object gate = new();

    // AesGcm (backed by an OpenSSL EVP cipher context on Linux) is not safe for concurrent
    // Encrypt/Decrypt calls on the same instance — this is a Singleton, and once 2FA setup started
    // encrypting a TOTP secret on every seeded test admin, xunit's parallel test execution began
    // hitting it from many threads at once and corrupting the shared cipher context
    // ("cipher operation failed"). A single instance is still cheap to serialize through; the
    // alternative (a fresh AesGcm per call) would just move the cost to re-deriving expensive AES
    // key schedules on every Protect/Unprotect instead.
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

        aes = new AesGcm(key, TagSize);
    }

    public string Protect(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        lock (gate)
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

        lock (gate)
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes); // throws CryptographicException on tamper/wrong key
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public void Dispose()
    {
        aes.Dispose();
        CryptographicOperations.ZeroMemory(key);
    }
}
