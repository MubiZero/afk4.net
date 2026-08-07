using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Результат обнаружения: сама запись, была ли она заведена сейчас и пора ли напомнить.</summary>
public sealed record IncidentTransition(PlatformIncidentEntity Incident, bool IsNew, bool ShouldRemind);

public interface IPlatformIncidentService
{
    Task<IncidentTransition> OpenOrTouchAsync(
        string kind, string dedupKey, string severity, string detailsJson, CancellationToken cancellationToken);

    /// <summary>Закрывает все открытые инциденты, ключей которых нет в переданном наборе.</summary>
    Task<IReadOnlyList<PlatformIncidentEntity>> ResolveMissingAsync(
        IReadOnlyCollection<string> stillOpenKeys, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformIncidentEntity>> ListOpenAsync(CancellationToken cancellationToken);
}
