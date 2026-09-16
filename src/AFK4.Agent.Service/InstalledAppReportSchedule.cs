namespace AFK4.Agent.Service;

/// <summary>
/// Решает, пора ли снова пересобирать список установленного софта.
///
/// Раньше инвентарь снимался ровно один раз, при старте службы: игру, поставленную днём, клуб
/// видел только после перезагрузки машины — то есть обычно никогда. Чистая функция: время и
/// интервал приходят снаружи, чтобы правило проверялось без работника и без ожидания.
/// </summary>
public static class InstalledAppReportSchedule
{
    /// <summary>Нижняя граница интервала: раз в минуту и не чаще, как бы ни настроили.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    public static TimeSpan Interval(int configuredMinutes) =>
        configuredMinutes < MinimumInterval.TotalMinutes
            ? MinimumInterval
            : TimeSpan.FromMinutes(configuredMinutes);

    public static bool IsDue(DateTimeOffset lastReportUtc, DateTimeOffset nowUtc, TimeSpan interval) =>
        nowUtc - lastReportUtc >= interval;
}
