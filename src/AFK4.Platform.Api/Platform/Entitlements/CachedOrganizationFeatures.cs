using Microsoft.Extensions.Caching.Memory;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Права организации для сердцебиения — с минутным кэшем.
///
/// Сердцебиение идёт каждые 3–10 с с каждого ПК клуба, а права меняются, когда владелец сменил
/// тариф или платформа переключила функцию. Четыре запроса на каждое сердцебиение ради ответа,
/// который меняется раз в месяц, — нагрузка без пользы; минута запаздывания после смены тарифа
/// экран игрока переживёт.
/// </summary>
public interface IOrganizationFeatureSnapshot
{
    Task<IReadOnlyList<string>> GetEnabledAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed class CachedOrganizationFeatures(IMemoryCache cache, IOrganizationEntitlements entitlements)
    : IOrganizationFeatureSnapshot
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public async Task<IReadOnlyList<string>> GetEnabledAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var key = $"organization-features:{organizationId:N}";
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var enabled = await entitlements.ListEnabledAsync(organizationId, cancellationToken);
        cache.Set(key, enabled, Lifetime);
        return enabled;
    }
}
