using AFK4.Platform.Api.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class MoneyFormattingTests
{
    [Theory]
    [InlineData(290000, "TJS", "2900.00")]
    [InlineData(0, "TJS", "0.00")]
    [InlineData(5, "TJS", "0.05")]
    [InlineData(-150000, "TJS", "-1500.00")]
    public void ToMajorString_TwoDecimalCurrency_FormatsInvariantly(long minorUnits, string currency, string expected) =>
        Assert.Equal(expected, MoneyFormatting.ToMajorString(minorUnits, currency));

    [Fact]
    public void ToMajorString_UnknownCurrency_FallsBackToTwoDecimals() =>
        Assert.Equal("12.34", MoneyFormatting.ToMajorString(1234, "XYZ"));
}
