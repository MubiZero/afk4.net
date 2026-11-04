#if !NETFRAMEWORK
using System.Security.Authentication;

namespace AFK4.Agent.Service.Network;

/// <summary>
/// Windows 7 сама предлагает по HTTPS только TLS 1.0 (у клиента TLS 1.2 выключен по умолчанию), а Cloudflare перед
/// платформой и обновлениями требует 1.2 — на стенде 04.11.2026 любой запрос падал с net_http_ssl_connection_failed.
/// Просим 1.2 явно, не трогая реестр клуба. На Windows 8 и новее версию выбирает система (там и TLS 1.3).
/// </summary>
public static class ClubTls
{
    private static readonly bool IsWindows7 = OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(6, 2);

    public static SocketsHttpHandler Apply(SocketsHttpHandler handler)
    {
        if (IsWindows7)
        {
            handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls12;
        }

        return handler;
    }

    /// <summary>Обработчик, который SignalR создаёт сам для согласования и SSE.</summary>
    public static HttpMessageHandler Apply(HttpMessageHandler handler)
    {
        if (IsWindows7 && handler is HttpClientHandler client)
        {
            client.SslProtocols = SslProtocols.Tls12;
        }

        return handler;
    }
}
#endif
