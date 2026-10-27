using System.Net;
using System.Net.Sockets;

namespace AFK4.Shared.Contracts.Games;

/// <summary>
/// Кеш игр клуба (LANCache, lancache.net): сервер в сети клуба, который качает игру из интернета один раз
/// и раздаёт её остальным ПК по локальной сети. Агент направляет к нему ПК через файл hosts — роутер клуба
/// перенастраивать не нужно (docs/operations/game-cache-lancache.md).
/// </summary>
/// <param name="Address">IPv4 кеш-сервера в сети клуба; null — кеша нет.</param>
/// <param name="DevicesSeeing">Сколько ПК филиала при последней проверке достучались до кеша.</param>
/// <param name="DevicesReporting">Сколько ПК вообще сообщили о проверке (агенты прежних версий молчат).</param>
public sealed record BranchGameCacheDto(string? Address, int DevicesSeeing, int DevicesReporting);

public sealed record UpdateBranchGameCacheRequest(string? Address);

public static class GameCacheNames
{
    /// <summary>По этому имени Steam сам находит кеш в сети клуба; если кеш не отвечает, качает из интернета.</summary>
    public const string SteamDiscoveryHost = "lancache.steamcontent.com";

    /// <summary>
    /// Имена, которые агент ведёт в hosts: Steam и Epic Games (список uklans/cache-domains). Riot и Battle.net
    /// раздают игры с имён по маске (*.dyn.riotcdn.net), а маску hosts не понимает — им нужен DNS клуба.
    /// </summary>
    public static readonly IReadOnlyList<string> HostsEntries =
    [
        SteamDiscoveryHost,
        "cdn.unrealengine.com",
        "cdn1.epicgames.com",
        "cdn1.unrealengine.com",
        "cdn2.epicgames.com",
        "cdn2.unrealengine.com",
        "cdn3.unrealengine.com",
        "cloudflare.epicgamescdn.com",
        "download.epicgames.com",
        "download2.epicgames.com",
        "download3.epicgames.com",
        "download4.epicgames.com",
        "egdownload.fastly-edge.com",
        "egs-cloudfront-chunks.epicgamescdn.com",
        "epicgames-download1.akamaized.net",
        "fastly-download.epicgames.com"
    ];

    /// <summary>Проверка живости LANCache (контейнер monolithic): отвечает 204 и заголовком X-LanCache-Processed-By.</summary>
    public const string HeartbeatPath = "/lancache-heartbeat";

    public const string HeartbeatHeader = "X-LanCache-Processed-By";

    /// <summary>
    /// Адрес кеша — только частный IPv4 сети клуба: hosts на всех ПК, указывающий в интернет, увёл бы
    /// загрузки игр на чужой сервер.
    /// </summary>
    public static bool IsValidAddress(string? address) =>
        IPAddress.TryParse(address, out var ip)
        && ip.AddressFamily == AddressFamily.InterNetwork
        && ip.ToString() == address
        && ip.GetAddressBytes() is var bytes
        && (bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168));
}
