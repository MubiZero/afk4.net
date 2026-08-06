using System;
using AFK4.Platform.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class AesGcmSecretProtectorTests
{
    // A throwaway 32-byte (all-zero) key, base64-encoded. Tests only.
    private const string TestKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private static AesGcmSecretProtector Create(string keyBase64 = TestKeyBase64) =>
        new(Options.Create(new SecretProtectionOptions { EncryptionKeyBase64 = keyBase64 }));

    [Fact]
    public void ProtectThenUnprotect_RoundTrips()
    {
        var protector = Create();
        const string secret = "dcg_super-secret-api-key-value";

        var protectedValue = protector.Protect(secret);

        Assert.NotEqual(secret, protectedValue);
        Assert.Equal(secret, protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertextEachTime()
    {
        var protector = Create();
        var a = protector.Protect("same-input");
        var b = protector.Protect("same-input");

        Assert.NotEqual(a, b); // random nonce per call
        Assert.Equal("same-input", protector.Unprotect(a));
        Assert.Equal("same-input", protector.Unprotect(b));
    }

    [Fact]
    public void Unprotect_WithTamperedCiphertext_Throws()
    {
        var protector = Create();
        var protectedValue = protector.Protect("secret");
        var parts = protectedValue.Split('.');
        var cipherBytes = Convert.FromBase64String(parts[2]);
        cipherBytes[0] ^= 0xFF;
        parts[2] = Convert.ToBase64String(cipherBytes);
        var tampered = string.Join('.', parts);

        Assert.ThrowsAny<Exception>(() => protector.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_WithWrongKey_Throws()
    {
        var enc = Create();
        var protectedValue = enc.Protect("secret");
        var other = Create("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb=");

        Assert.ThrowsAny<Exception>(() => other.Unprotect(protectedValue));
    }

    [Fact]
    public void Constructor_WithShortKey_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            new AesGcmSecretProtector(Options.Create(new SecretProtectionOptions
            {
                EncryptionKeyBase64 = Convert.ToBase64String(new byte[16]) // 16 bytes, not 32
            })));
    }

    // Fixed vector: key=all-zero 32 bytes, nonce=all-zero 12 bytes, plaintext="golden-vector-secret",
    // produced with the all-zero test key above. AES-GCM is stateless per Encrypt/Decrypt call — the
    // envelope format and ciphertext for a given key/nonce/plaintext do not depend on whether the
    // AesGcm instance is reused or freshly constructed, so this pins the wire format itself and
    // guards against a future change (e.g. instance lifetime, tag size, nonce size) silently breaking
    // decryption of values persisted before that change.
    private const string GoldenVector = "v1.AAAAAAAAAAAAAAAA.qcgsWSgORhhiLbG8yN7ufRESZr4=.5L5rMpx+z9hUWuRSeshf8w==";

    [Fact]
    public void Unprotect_GoldenVector_DecryptsToKnownPlaintext()
    {
        var protector = Create();

        Assert.Equal("golden-vector-secret", protector.Unprotect(GoldenVector));
    }

    // The regression this guards: AesGcm's OpenSSL-backed cipher context is not safe for concurrent
    // Encrypt/Decrypt on one shared instance. This used to be a Singleton reusing one AesGcm — under
    // enough concurrent traffic that corrupted the shared context ("cipher operation failed"). Protect
    // now constructs a fresh AesGcm per call, so this must stay clean under real concurrency.
    [Fact]
    public async Task ConcurrentProtectAndUnprotect_DoesNotThrow()
    {
        var protector = Create();

        var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            var plaintext = $"concurrent-secret-{i}";
            var protectedValue = protector.Protect(plaintext);
            Assert.Equal(plaintext, protector.Unprotect(protectedValue));
        }));

        await Task.WhenAll(tasks);
    }
}
