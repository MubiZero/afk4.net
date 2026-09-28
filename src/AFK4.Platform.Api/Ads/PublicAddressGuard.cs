using System.Net;
using System.Net.Sockets;

namespace AFK4.Platform.Api.Ads;

/// <summary>
/// Соединение только с публичными адресами. Картинку рекламы сервер скачивает по ссылке, которую
/// ввёл человек: без этой проверки ссылка на 127.0.0.1, 10.x или адрес метаданных облака заставила бы
/// сервер обратиться внутрь своей же сети. Проверка — в момент соединения, а не по тексту ссылки:
/// так она ловит и переадресацию, и имя, которое резолвится во внутренний адрес.
/// </summary>
public static class PublicAddressGuard
{
    public static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = ConnectAsync,
        // Переадресации разрешены: каждое новое соединение снова проходит ConnectCallback.
        MaxAutomaticRedirections = 3
    };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var target = addresses.FirstOrDefault(IsPublic)
            ?? throw new HttpRequestException($"Refusing to connect to a non-public address of '{context.DnsEndPoint.Host}'.");
        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0
                || b[0] == 10
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) // CGNAT
                || (b[0] == 169 && b[1] == 254) // link-local, метаданные облака
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                || b[0] >= 224); // multicast и зарезервированные
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC // unique local fc00::/7
                || address.Equals(IPAddress.IPv6None));
        }

        return false;
    }
}
