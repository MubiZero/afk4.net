using System.Text.RegularExpressions;
using AFK4.Platform.Api.Identity.PhoneOtp;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PhoneOtpHasherAndGeneratorTests
{
    [Fact]
    public void Hash_IsDeterministic_LowercaseHex64()
    {
        var hasher = new Sha256PhoneOtpHasher();

        var a = hasher.Hash("123456");
        var b = hasher.Hash("123456");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void Hash_DiffersForDifferentCodes()
    {
        var hasher = new Sha256PhoneOtpHasher();
        Assert.NotEqual(hasher.Hash("123456"), hasher.Hash("654321"));
    }

    [Fact]
    public void Generate_ProducesSixDigitCodes()
    {
        var generator = new RandomPhoneOtpGenerator();

        for (var i = 0; i < 200; i++)
        {
            var code = generator.Generate();
            Assert.Matches("^[0-9]{6}$", code);
        }
    }
}
