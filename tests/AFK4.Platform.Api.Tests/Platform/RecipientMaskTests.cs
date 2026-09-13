using AFK4.Platform.Api.Platform.Health;

namespace AFK4.Platform.Api.Tests.Platform;

// Разбору доставки нужен домен — «на gmail.com не уходит» видно и по нему. Полный адрес человека
// для этого не нужен, поэтому в список провалов он не попадает.
public sealed class RecipientMaskTests
{
    [Theory]
    [InlineData("info@mubi.dev", "i***@mubi.dev")]
    [InlineData("  owner@club.example  ", "o***@club.example")]
    [InlineData("a@mubi.dev", "*@mubi.dev")]
    public void Email_KeepsTheDomainAndHidesThePerson(string address, string expected)
    {
        Assert.Equal(expected, RecipientMask.Apply(address));
    }

    [Fact]
    public void Phone_KeepsTheCountryCodeOnly()
    {
        var masked = RecipientMask.Apply("+992937380070");

        Assert.StartsWith("+992", masked);
        Assert.EndsWith("70", masked);
        Assert.DoesNotContain("937380", masked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoAddress_IsEmpty(string? address)
    {
        Assert.Equal(string.Empty, RecipientMask.Apply(address));
    }
}
