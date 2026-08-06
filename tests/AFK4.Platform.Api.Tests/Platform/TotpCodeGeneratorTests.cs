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

    // Секрет из 20 байт кратен 5, поэтому хвостовая ветка ToBase32 (добивание неполной
    // 5-битной группы) тестом выше не исполняется ни разу. Ниже — секреты длиной, не кратной
    // 5 байтам, чтобы эта ветка реально проходила проверку.
    [Theory]
    // 1 байт 0xFF = 11111111b. Группы по 5 бит: 11111 | 111(00) -> 31,28 -> '7','4'.
    [InlineData(new byte[] { 0xFF }, "74")]
    // 16 байт ASCII "1234567890123456" — не кратно 5 (16 % 5 == 1).
    [InlineData(
        new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36 },
        "GEZDGNBVGY3TQOJQGEZDGNBVGY")]
    // 21 байт ASCII "123456789012345678901" — не кратно 5 (21 % 5 == 1).
    [InlineData(
        new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0x31 },
        "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGE")]
    public void ToBase32_PadsFinalIncompleteGroup_ForLengthsNotMultipleOfFive(byte[] secret, string expected)
    {
        Assert.Equal(expected, TotpCodeGenerator.ToBase32(secret));
    }
}
