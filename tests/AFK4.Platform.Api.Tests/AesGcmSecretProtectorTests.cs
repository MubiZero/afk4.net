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
        var tampered = protectedValue[..^2] + (protectedValue.EndsWith("A") ? "B=" : "A=");

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
}
