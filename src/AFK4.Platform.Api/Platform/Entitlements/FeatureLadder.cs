using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Сама лестница «ручное исключение для клуба → мнение тарифа → умолчание фичи», отдельно от
/// того, как достаются три её ступени. И полный обход каталога (<see cref="EfOrganizationEntitlements.DescribeAsync"/>),
/// и точечная проверка одной фичи (<see cref="EfOrganizationEntitlements.IsEnabledAsync"/>) обязаны
/// звать эту функцию — иначе правило начнёт жить в двух местах и однажды разойдётся молча.
/// </summary>
public static class FeatureLadder
{
    public static (bool IsEnabled, string DecisionLevel) Resolve(bool? overrideValue, bool? planValue, bool? defaultValue)
    {
        if (overrideValue is { } fromOverride)
        {
            return (fromOverride, FeatureDecisionLevels.Override);
        }

        if (planValue is { } fromPlan)
        {
            return (fromPlan, FeatureDecisionLevels.Plan);
        }

        // Незнакомый ключ фичи приходит сюда с defaultValue == null — тоже «выключено».
        return (defaultValue ?? false, FeatureDecisionLevels.Default);
    }
}
