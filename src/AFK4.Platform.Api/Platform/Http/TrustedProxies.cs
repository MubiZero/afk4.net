using Microsoft.AspNetCore.HttpOverrides;

namespace AFK4.Platform.Api.Platform.Http;

/// <summary>
/// Кому API верит в <c>X-Forwarded-For</c>. В Coolify запрос приходит через Traefik: без разбора
/// заголовка адресом клиента для API был бы адрес прокси, и каждое ограничение частоты «по IP»
/// считало бы один общий счётчик на всю сеть — вход сотрудников всех клубов упирался бы в десять
/// попыток в минуту на всех.
///
/// Верим только соседу из внутренней сети (docker, loopback) и только на один шаг: Traefik сам
/// перезаписывает заголовок адресом клиента. Заголовок от внешнего адреса не значит ничего —
/// иначе любой обходил бы лимит, подставив чужой адрес. Сети можно сузить настройкой
/// <c>ForwardedHeaders:KnownNetworks</c> (CIDR), например до подсети Coolify.
/// </summary>
public static class TrustedProxies
{
    public const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";

    public static readonly IReadOnlyList<string> DefaultNetworks =
    [
        "127.0.0.0/8",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "::1/128",
        "fc00::/7",
        "fe80::/10"
    ];

    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var configured = configuration.GetSection(KnownNetworksKey).Get<string[]>();
        foreach (var network in configured is { Length: > 0 } ? configured : DefaultNetworks)
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
