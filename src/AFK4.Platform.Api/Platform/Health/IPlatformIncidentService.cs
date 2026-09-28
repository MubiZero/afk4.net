using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Результат обнаружения: сама запись, была ли она заведена сейчас и пора ли напомнить.</summary>
public sealed record IncidentTransition(PlatformIncidentEntity Incident, bool IsNew, bool ShouldRemind);
