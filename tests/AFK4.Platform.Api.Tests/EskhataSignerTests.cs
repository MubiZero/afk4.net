using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataSignerTests
{
    // Канонический вектор из спеки Eskhata (раздел 2): orderTypeId=1.
    // Sha256("00116.00972description125581.56770fcaaed849dd8c80f41d5dd938e7)".
    [Fact]
    public void BuildHash_MatchesBankDocVector()
    {
        var values = new[] { "001", "16.00", "972", "description", "12558", "1" };
        var hash = EskhataSigner.BuildHash(values, "56770fcaaed849dd8c80f41d5dd938e7");
        Assert.Equal("9b9a46632e1dc5850d35bca1760479e6f0ccad290904f69cfc1723e89a1a6cc5", hash);
    }

    [Fact]
    public void CompanyIdHeader_IsBase64OfRawId()
    {
        Assert.Equal("YWJj", EskhataSigner.CompanyIdHeader("abc"));
    }

    [Theory]
    [InlineData(1600, "16.00")]
    [InlineData(99, "0.99")]
    [InlineData(100000, "1000.00")]
    public void FormatAmount_TwoDecimalsInvariant(long minor, string expected)
        => Assert.Equal(expected, EskhataSigner.FormatAmount(minor));
}
