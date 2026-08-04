using AFK4.Platform.Api.Platform.Identity;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class TotpCodeGeneratorTests
{
    private static readonly byte[] Secret = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Generate_MatchesRfc6238Vectors(long unixTime, string expected)
    {
        Assert.Equal(expected, TotpCodeGenerator.Generate(Secret, unixTime));
    }

    [Fact]
    public void Verify_AcceptsPreviousAndNextStep_ButNotTwoStepsAway()
    {
        var code = TotpCodeGenerator.Generate(Secret, 1234567890L);

        Assert.True(TotpCodeGenerator.Verify(Secret, code, 1234567890L + 30));
        Assert.True(TotpCodeGenerator.Verify(Secret, code, 1234567890L - 30));
        Assert.False(TotpCodeGenerator.Verify(Secret, code, 1234567890L + 90));
    }

    [Fact]
    public void ToBase32_ProducesRfc4648AlphabetWithoutPadding()
    {
        var encoded = TotpCodeGenerator.ToBase32(Secret);

        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", encoded);
    }
}
