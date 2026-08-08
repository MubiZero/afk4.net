namespace AFK4.Shared.Contracts.Platform.Features;

/// <summary>Ступень лестницы, на которой принято решение о фиче.</summary>
public static class FeatureDecisionLevels
{
    public const string Override = "override";

    public const string Plan = "plan";

    public const string Default = "default";
}

/// <summary>
/// Состояние фичи для клуба вместе с тем, ЧЕМ оно решено: «не куплено» и «не выкачено» —
/// разные ответы клиенту, и панель обязана их различать.
/// </summary>
public sealed record OrganizationFeatureStateDto(
    string FeatureKey,
    string Name,
    string Description,
    bool IsEnabled,
    string DecisionLevel,
    bool? OverrideValue,
    string? OverrideReason,
    DateTimeOffset? OverrideSetAtUtc,
    bool? PlanValue,
    bool DefaultValue);

/// <summary>Постановка ручного исключения для клуба. Причина обязательна.</summary>
public sealed record SetFeatureOverrideRequest(bool IsEnabled, string Reason);

/// <summary>Список включённых фич для клубского приложения.</summary>
public sealed record EnabledFeaturesDto(IReadOnlyList<string> Features);
