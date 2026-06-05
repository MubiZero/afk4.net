using AFK4.Platform.Api.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("+992 93 738-00-70", "992937380070")]
    [InlineData("992937380070", "992937380070")]
    [InlineData("+992-93-738-00-70", "992937380070")]
    [InlineData("  +7 (916) 123-45-67 ", "79161234567")]
    public void Normalize_StripsFormatting_KeepsDigits(string raw, string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("937380070")]            // 9 digits — no country code
    [InlineData("12345")]                // too short
    [InlineData("9929373800701234567")]  // 19 digits — too long
    [InlineData("abc-def")]              // no digits
    public void Normalize_RejectsInvalid_ReturnsNull(string? raw)
    {
        Assert.Null(PhoneNumberNormalizer.Normalize(raw));
    }
}
