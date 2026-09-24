using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AFK4.Agent.Service.Network;

public static class MagicPacket
{
    /// <summary>Волшебный пакет: шесть 0xFF и шестнадцать раз MAC того, кого будим.</summary>
    public static byte[] Build(PhysicalAddress mac)
    {
        var address = mac.GetAddressBytes();
        if (address.Length != 6)
        {
            throw new ArgumentException("A wake-on-LAN target needs a 6-byte MAC address.", nameof(mac));
        }

        var packet = new byte[6 + 16 * 6];
        packet.AsSpan(0, 6).Fill(0xFF);
        for (var repeat = 0; repeat < 16; repeat++)
        {
            address.CopyTo(packet, 6 + repeat * 6);
        }

        return packet;
    }

    /// <summary>MAC из «AA-BB-CC-DD-EE-FF» или «AA:BB:CC:DD:EE:FF»; только шесть байт.</summary>
    public static bool TryParseMac(string? value, out PhysicalAddress mac)
    {
        mac = PhysicalAddress.None;
        if (string.IsNullOrWhiteSpace(value) || !PhysicalAddress.TryParse(value.Trim(), out var parsed)
            || parsed.GetAddressBytes().Length != 6)
        {
            return false;
        }

        mac = parsed;
        return true;
    }
}

public interface IWakeOnLanSender
{
    Task SendAsync(PhysicalAddress mac, IPAddress broadcast, CancellationToken cancellationToken);
}

public sealed class UdpWakeOnLanSender : IWakeOnLanSender
{
    public const int Port = 9;

    public async Task SendAsync(PhysicalAddress mac, IPAddress broadcast, CancellationToken cancellationToken)
    {
        var packet = MagicPacket.Build(mac);
        using var client = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        var target = new IPEndPoint(broadcast, Port);
        // UDP без подтверждения: три копии — обычная страховка от потерянного кадра.
        for (var copy = 0; copy < 3; copy++)
        {
            await client.SendAsync(packet, target, cancellationToken);
        }
    }
}
