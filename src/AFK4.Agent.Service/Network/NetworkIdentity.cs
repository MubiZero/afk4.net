using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AFK4.Agent.Service.Network;

/// <summary>Сетевой адрес этого ПК: по нему выключенную машину будит сосед (спека оболочки, §5.8).</summary>
public sealed record NetworkIdentity(string MacAddress, string Subnet, string BroadcastAddress);

public interface INetworkIdentityProvider
{
    /// <summary>null — подходящего адаптера нет (ни одного со шлюзом) или его не удалось прочитать.</summary>
    NetworkIdentity? Current { get; }
}

public static class NetworkMath
{
    /// <summary>
    /// Подсеть «192.168.1.0/24» и широковещательный адрес «192.168.1.255» по адресу и маске.
    /// Будить можно только из той же подсети: волшебный пакет не проходит маршрутизатор.
    /// </summary>
    public static (string Subnet, string Broadcast) Describe(IPAddress address, IPAddress mask)
    {
        var ip = ToUInt32(address);
        var netmask = ToUInt32(mask);
        var network = ip & netmask;
        var broadcast = network | ~netmask;
        var prefix = System.Numerics.BitOperations.PopCount(netmask);
        return ($"{FromUInt32(network)}/{prefix}", FromUInt32(broadcast).ToString());
    }

    /// <summary>MAC в виде «AA-BB-CC-DD-EE-FF» — так его ждёт сервер и так его пишет Windows.</summary>
    public static string FormatMac(PhysicalAddress address) =>
        string.Join('-', address.GetAddressBytes().Select(value => value.ToString("X2")));

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    private static IPAddress FromUInt32(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}

/// <summary>
/// Адаптер, через который ПК ходит в сеть: работает, не петля и не туннель, есть IPv4 со шлюзом.
/// Проводной — первым: будить по Wi-Fi Windows почти никогда не умеет. Читается раз в пять минут:
/// сердцебиение стучит каждые несколько секунд, а адрес меняется, когда ПК переставили.
/// </summary>
public sealed class SystemNetworkIdentityProvider(TimeProvider timeProvider, ILogger<SystemNetworkIdentityProvider> logger)
    : INetworkIdentityProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private readonly Lock gate = new();
    private NetworkIdentity? cached;
    private DateTimeOffset readAtUtc = DateTimeOffset.MinValue;

    public NetworkIdentity? Current
    {
        get
        {
            lock (gate)
            {
                var now = timeProvider.GetUtcNow();
                if (now - readAtUtc < CacheLifetime)
                {
                    return cached;
                }

                cached = Read();
                readAtUtc = now;
                return cached;
            }
        }
    }

    private NetworkIdentity? Read()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up
                    && adapter.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .Select(adapter => (Adapter: adapter, Properties: adapter.GetIPProperties()))
                .Where(item => item.Properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any)))
                .OrderBy(item => item.Adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1);

            foreach (var (adapter, properties) in candidates)
            {
                var unicast = properties.UnicastAddresses
                    .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork);
                var mac = adapter.GetPhysicalAddress();
                if (unicast?.IPv4Mask is null || mac.GetAddressBytes().Length != 6)
                {
                    continue;
                }

                var (subnet, broadcast) = NetworkMath.Describe(unicast.Address, unicast.IPv4Mask);
                return new NetworkIdentity(NetworkMath.FormatMac(mac), subnet, broadcast);
            }
        }
        catch (Exception exception) when (exception is NetworkInformationException or PlatformNotSupportedException)
        {
            logger.LogDebug(exception, "Network identity could not be read.");
        }

        return null;
    }
}
