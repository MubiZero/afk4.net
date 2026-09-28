using System.Net;
using AFK4.Platform.Api.Ads;

namespace AFK4.Platform.Api.Tests.Ads;

/// <summary>Картинку рекламы сервер скачивает только с публичных адресов — не изнутри своей сети.</summary>
public sealed class PublicAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.20.1.1")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.5")]
    public void InternalAddresses_AreRefused(string address)
    {
        Assert.False(PublicAddressGuard.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("207.180.237.97")]
    [InlineData("2001:4860:4860::8888")]
    public void PublicAddresses_AreAllowed(string address)
    {
        Assert.True(PublicAddressGuard.IsPublic(IPAddress.Parse(address)));
    }
}
