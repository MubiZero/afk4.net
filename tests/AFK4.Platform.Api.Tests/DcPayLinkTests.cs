using AFK4.Platform.Api.Payments.Dc;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcPayLinkTests
{
    [Fact]
    public void FormatAmount_TwoDecimals_Invariant()
    {
        Assert.Equal("50.00", DcPayLink.FormatAmount(5000));
        Assert.Equal("0.05", DcPayLink.FormatAmount(5));
        Assert.Equal("1234.50", DcPayLink.FormatAmount(123450));
    }

    [Fact]
    public void BuildComment_SubstitutesRef()
    {
        Assert.Equal("AFK4-1a2b3c4d", DcPayLink.BuildComment("AFK4-{ref}", "1a2b3c4d"));
    }

    [Fact]
    public void BuildUrl_HasCardAmountEncodedCommentAndConstant()
    {
        var url = DcPayLink.BuildUrl("1234567890123456", 5000, "AFK4 заказ 7");
        Assert.Equal(
            "http://pay.dc.tj/?A=1234567890123456&s=50.00&c=AFK4%20%D0%B7%D0%B0%D0%BA%D0%B0%D0%B7%207&f1=133",
            url);
    }
}
