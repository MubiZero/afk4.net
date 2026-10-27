using System.Net;
using System.Net.Sockets;

namespace AFK4.Shared.Contracts.Games;

/// <summary>
/// Кеш игр клуба: качает игру из интернета один раз и раздаёт её остальным ПК по локальной сети. По умолчанию
/// он встроен в агента ПК администратора — клубу ничего не ставить; большой клуб может указать свой сервер
/// LANCache. Агент направляет к кешу ПК через файл hosts — роутер клуба не трогаем
/// (docs/operations/game-cache-lancache.md).
/// </summary>
/// <param name="Address">Свой сервер LANCache клуба (IPv4 в сети клуба); null — кеш встроенный, на ПК администратора.</param>
/// <param name="DevicesSeeing">Сколько ПК филиала при последней проверке достучались до кеша.</param>
/// <param name="DevicesReporting">Сколько ПК вообще сообщили о проверке (агенты прежних версий молчат).</param>
/// <param name="Enabled">Кеш включён. По умолчанию — да: клубу ничего настраивать не нужно.</param>
/// <param name="HostName">Имя ПК администратора со встроенным кешем; null — такого ПК на связи нет или задан свой сервер.</param>
/// <param name="EffectiveAddress">Куда сейчас идут игровые ПК; null — некуда, качают напрямую.</param>
/// <param name="StoredBytes">Сколько лежит во встроенном кеше.</param>
public sealed record BranchGameCacheDto(
    string? Address,
    int DevicesSeeing,
    int DevicesReporting,
    bool Enabled = true,
    string? HostName = null,
    string? EffectiveAddress = null,
    long StoredBytes = 0);

/// <param name="Address">Свой сервер LANCache клуба; null — встроенный кеш на ПК администратора.</param>
public sealed record UpdateBranchGameCacheRequest(string? Address, bool Enabled = true);

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
