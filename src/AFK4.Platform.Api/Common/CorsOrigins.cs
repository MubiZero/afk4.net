using Microsoft.Extensions.Configuration;

namespace AFK4.Platform.Api.Common;

/// <summary>
/// Сборка списка источников для CORS. Встроенные умолчания — это адреса разработчика:
/// loopback-порты Vite и preview плюс hosts-имена <c>*.afk4.local</c>. Они удобны: браузерная
/// сборка кабинета работает против любого стенда без правки его конфигурации. Но в проде они
/// означают, что страница, открытая на машине оператора по такому адресу, может читать ответы
/// API — токен ей ещё нужно раздобыть (он в sessionStorage, cookie-аутентификации в API нет),
/// но давать ей фору незачем. Поэтому в Production умолчания не подставляются вовсе: прод
/// перечисляет свои источники в конфигурации явно.
/// </summary>
public static class CorsOrigins
{
    public static string[] Resolve(
        IConfiguration configuration,
        string configurationKey,
        string[] developerDefaults,
        bool allowDeveloperDefaults)
    {
        var configured = configuration.GetSection(configurationKey).Get<string[]>() ?? [];
        var defaults = allowDeveloperDefaults ? developerDefaults : [];

        return defaults
            .Concat(configured)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
