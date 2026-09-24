using System.Net;
using System.Net.NetworkInformation;
using AFK4.Agent.Service.Network;

namespace AFK4.Agent.Service.Tests;

/// <summary>Подсеть и широковещательный адрес: по ним сервер выбирает, кем будить выключенный ПК.</summary>
public sealed class NetworkIdentityTests
{
    [Theory]
    [InlineData("192.168.1.37", "255.255.255.0", "192.168.1.0/24", "192.168.1.255")]
    [InlineData("10.0.5.9", "255.255.252.0", "10.0.4.0/22", "10.0.7.255")]
    [InlineData("172.16.200.1", "255.255.0.0", "172.16.0.0/16", "172.16.255.255")]
    public void Describe_GivesTheSubnetAndItsBroadcast(string address, string mask, string subnet, string broadcast)
    {
        var described = NetworkMath.Describe(IPAddress.Parse(address), IPAddress.Parse(mask));

        Assert.Equal(subnet, described.Subnet);
        Assert.Equal(broadcast, described.Broadcast);
    }

    [Fact]
    public void Mac_IsWrittenTheWayWindowsAndTheServerWriteIt()
    {
        Assert.Equal("AA-BB-0C-DD-EE-01", NetworkMath.FormatMac(PhysicalAddress.Parse("AABB0CDDEE01")));
    }

    [Fact]
    public void TheMagicPacket_IsSixFfsAndSixteenCopiesOfTheMac()
    {
        var mac = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");

        var packet = MagicPacket.Build(mac);

        Assert.Equal(102, packet.Length);
        Assert.All(packet[..6], value => Assert.Equal(0xFF, value));
        for (var copy = 0; copy < 16; copy++)
        {
            Assert.Equal(mac.GetAddressBytes(), packet[(6 + copy * 6)..(12 + copy * 6)]);
        }
    }

    [Theory]
    [InlineData("AA-BB-CC-DD-EE-FF", true)]
    [InlineData("aa:bb:cc:dd:ee:ff", true)]
    [InlineData("AA-BB-CC-DD-EE", false)]
    [InlineData("AA-BB-CC-DD-EE-FF-00-11", false)]
    [InlineData("not a mac", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyASixByteMac_IsAWakeTarget(string? value, bool accepted)
    {
        Assert.Equal(accepted, MagicPacket.TryParseMac(value, out _));
    }
}
