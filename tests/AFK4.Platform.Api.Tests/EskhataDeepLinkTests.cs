using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataDeepLinkTests
{
    [Fact]
    public void FromInvoiceUrl_BuildsSchemeFromLastSegment()
        => Assert.Equal("eskhata://pay/hlR2oH",
            EskhataDeepLink.FromInvoiceUrl("https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public void FromInvoiceUrl_ReturnsNullOnUnparseable(string? input)
        => Assert.Null(EskhataDeepLink.FromInvoiceUrl(input));
}
