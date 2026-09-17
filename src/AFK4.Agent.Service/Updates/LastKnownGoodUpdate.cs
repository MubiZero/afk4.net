namespace AFK4.Agent.Service.Updates;

/// <summary>
/// Пакет версии компонента, которая реально встала на эту машину.
///
/// Единственное, на что можно откатиться, когда следующая версия оказалась сломанной. Раньше этой
/// записи не было вовсе, и «откат» запускал тот же самый пакет, который только что не установился:
/// в журнале появлялось «откатились», а на машине не менялось ничего.
/// </summary>
public sealed record LastKnownGoodUpdate(
    string Component,
    string Version,
    string ArtifactPath,
    DateTimeOffset RecordedAtUtc);

/// <summary>Куда откатываться и что сказать, если откатываться некуда.</summary>
public static class UpdateRollbackPlan
{
    public const string NoTargetMessage =
        "Rollback skipped: no previously installed package is kept on this device.";

    /// <summary>
    /// Состояние отката, нацеленное на последнюю удачную версию. <c>null</c>, если её пакета на
    /// диске нет: тогда честнее сказать «откатить нечем», чем переустановить сломанное.
    /// </summary>
    public static UpdateInstallState? ToKnownGood(UpdateInstallState state, LastKnownGoodUpdate? lastKnownGood)
    {
        if (lastKnownGood is null
            || string.IsNullOrWhiteSpace(lastKnownGood.ArtifactPath)
            || !File.Exists(lastKnownGood.ArtifactPath))
        {
            return null;
        }

        return state with
        {
            TargetVersion = lastKnownGood.Version,
            ArtifactPath = lastKnownGood.ArtifactPath
        };
    }
}
