namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>Филиал в том объёме, в каком свёртке нужно знать о нём.</summary>
public sealed record BranchSnapshotBranch(
    Guid BranchId,
    Guid OrganizationId,
    string TimeZoneId,
    DateTimeOffset CreatedAtUtc);

/// <summary>Событие без денег: старт сеанса, открытие смены.</summary>
public sealed record BranchSnapshotEvent(Guid BranchId, DateTimeOffset AtUtc);

/// <summary>Денежная строка: платёж или запись реестра.</summary>
public sealed record BranchSnapshotMoney(
    Guid BranchId,
    DateTimeOffset AtUtc,
    string Kind,
    long AmountMinorUnits,
    string CurrencyCode);

public sealed record BranchSnapshotInput(
    DateTimeOffset Now,
    IReadOnlyList<BranchSnapshotBranch> Branches,
    IReadOnlyDictionary<Guid, DateOnly> LastSnapshotDates,
    IReadOnlyList<BranchSnapshotEvent> SessionStarts,
    IReadOnlyList<BranchSnapshotMoney> Payments,
    IReadOnlyList<BranchSnapshotMoney> LedgerEntries,
    IReadOnlyList<BranchSnapshotEvent> ShiftOpens,
    IReadOnlyDictionary<Guid, DateTimeOffset> LastHeartbeatUtc);

public sealed record BranchDayFacts(
    Guid BranchId,
    Guid OrganizationId,
    DateOnly Date,
    int SessionCount,
    long RevenueMinorUnits,
    string CurrencyCode,
    int ShiftOpenedCount,
    bool? AgentAlive);

/// <summary>
/// Свёртка суток филиала. Чистая функция: раннер отвечает за запросы, эта — за правила, и её
/// правила (граница суток, «неизвестно» вместо выдуманного нуля) проверяются без базы.
/// </summary>
public static class BranchDailySnapshotBuilder
{
    /// <summary>Насколько глубоко задание готово доснять пропущенные дни после простоя.</summary>
    public const int MaxBackfillDays = 30;

    /// <summary>Порог живости: heartbeat старше суток — клуб на связь не выходил.</summary>
    private static readonly TimeSpan HeartbeatWindow = TimeSpan.FromDays(1);

    public static IReadOnlyList<BranchDayFacts> Build(BranchSnapshotInput input)
    {
        var sessionsByBranch = input.SessionStarts.ToLookup(item => item.BranchId);
        var paymentsByBranch = input.Payments.ToLookup(item => item.BranchId);
        var ledgerByBranch = input.LedgerEntries.ToLookup(item => item.BranchId);
        var shiftsByBranch = input.ShiftOpens.ToLookup(item => item.BranchId);

        var facts = new List<BranchDayFacts>();

        foreach (var branch in input.Branches)
        {
            var zone = BranchLocalTime.ResolveZone(branch.TimeZoneId);
            var localToday = BranchLocalTime.LocalDate(input.Now, zone);
            var lastCompleteDay = localToday.AddDays(-1);

            var startDay = input.LastSnapshotDates.TryGetValue(branch.BranchId, out var lastDate)
                ? lastDate.AddDays(1)
                : lastCompleteDay;

            // Досъёмка ограничена: чем дальше в прошлое, тем меньше оснований доверять
            // реконструкции задним числом.
            var earliest = lastCompleteDay.AddDays(-MaxBackfillDays);
            if (startDay < earliest) startDay = earliest;

            // И не раньше, чем филиал появился: сутки до его создания — не «ноль выручки»,
            // а отсутствие клуба.
            var born = BranchLocalTime.LocalDate(branch.CreatedAtUtc, zone);
            if (startDay < born) startDay = born;

            if (startDay > lastCompleteDay) continue;

            var sessionsByDay = GroupEvents(sessionsByBranch[branch.BranchId], zone);
            var shiftsByDay = GroupEvents(shiftsByBranch[branch.BranchId], zone);
            var paymentsByDay = GroupMoney(paymentsByBranch[branch.BranchId], zone);
            var ledgerByDay = GroupMoney(ledgerByBranch[branch.BranchId], zone);
            var agentAliveNow = ResolveAgentAlive(input, branch.BranchId);

            for (var day = startDay; day <= lastCompleteDay; day = day.AddDays(1))
            {
                var dayPayments = paymentsByDay.TryGetValue(day, out var paid) ? paid : [];
                var dayLedger = ledgerByDay.TryGetValue(day, out var entries) ? entries : [];

                facts.Add(new BranchDayFacts(
                    branch.BranchId,
                    branch.OrganizationId,
                    day,
                    sessionsByDay.TryGetValue(day, out var sessions) ? sessions : 0,
                    BranchRevenue.PosNet(dayPayments.Select(item => (item.Kind, item.AmountMinorUnits)))
                        + BranchRevenue.Gameplay(dayLedger.Select(item => (item.Kind, item.AmountMinorUnits))),
                    ResolveCurrency(dayPayments, dayLedger),
                    shiftsByDay.TryGetValue(day, out var shifts) ? shifts : 0,
                    // Живость меряется только «сейчас» — heartbeat перезаписывается. Поэтому она
                    // ставится единственным суткам, которые только что закончились; доснятым
                    // задним числом честно остаётся «неизвестно».
                    day == lastCompleteDay ? agentAliveNow : null));
            }
        }

        return facts;
    }

    private static bool? ResolveAgentAlive(BranchSnapshotInput input, Guid branchId)
    {
        // Ни одного устройства не заведено — про связь клуба сказать нечего, и «мёртв» здесь
        // было бы неправдой про клуб, который просто ещё не разворачивали.
        if (!input.LastHeartbeatUtc.TryGetValue(branchId, out var heartbeat)) return null;
        return input.Now - heartbeat <= HeartbeatWindow;
    }

    private static string ResolveCurrency(
        IReadOnlyList<BranchSnapshotMoney> payments,
        IReadOnlyList<BranchSnapshotMoney> ledger) =>
        payments.FirstOrDefault()?.CurrencyCode
        ?? ledger.FirstOrDefault()?.CurrencyCode
        ?? BranchRevenue.DefaultCurrencyCode;

    private static Dictionary<DateOnly, int> GroupEvents(IEnumerable<BranchSnapshotEvent> events, TimeZoneInfo zone) =>
        events
            .GroupBy(item => BranchLocalTime.LocalDate(item.AtUtc, zone))
            .ToDictionary(group => group.Key, group => group.Count());

    private static Dictionary<DateOnly, IReadOnlyList<BranchSnapshotMoney>> GroupMoney(
        IEnumerable<BranchSnapshotMoney> rows,
        TimeZoneInfo zone) =>
        rows
            .GroupBy(item => BranchLocalTime.LocalDate(item.AtUtc, zone))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<BranchSnapshotMoney>)group.ToList());
}
