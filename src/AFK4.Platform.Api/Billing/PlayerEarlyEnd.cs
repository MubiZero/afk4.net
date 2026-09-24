using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Что вернётся игроку, если встать сейчас: деньги за неиграное предоплаченное время и минуты
/// пакета, списанные вперёд.
/// </summary>
public sealed record PlayerEarlyEndQuote(
    int BilledMinutes,
    string CurrencyCode,
    PrepaidEarlyEndSettlement Money,
    Guid? ChargeEntryId,
    Guid? CashbackEntryId,
    Guid? PlayerPackageId,
    int PackageIncludedSecondsReturned,
    int PackageBonusSecondsReturned,
    Guid? PackageIncludedEntryId,
    Guid? PackageBonusEntryId)
{
    public static PlayerEarlyEndQuote Nothing(string currencyCode) =>
        new(0, currencyCode, new PrepaidEarlyEndSettlement(0, 0), null, null, null, 0, 0, null, null);

    public int PackageSecondsReturned => PackageIncludedSecondsReturned + PackageBonusSecondsReturned;
}

/// <summary>
/// Ранний выход игрока — один расчёт на два места: «сколько вернётся» до нажатия и сам возврат.
/// Две копии этой арифметики однажды разошлись бы, и разошлись бы в деньгах.
///
/// Предоплаченная сессия и пакет списываются вперёд целиком, поэтому ранний выход — не доплата, а
/// возврат переплаты. Сыгранным считается время от старта за вычетом пауз: паузу ставит
/// администратор (#279), и раньше игрок платил и за неё — расчёт брал «сейчас минус старт».
/// </summary>
public static class PlayerEarlyEnd
{
    private const string PackageTariffPrefix = "package:";

    public static async Task<PlayerEarlyEndQuote> QuoteAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        Guid playerAccountId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (session.StartedAtUtc is not { } startedAt)
        {
            return PlayerEarlyEndQuote.Nothing("TJS");
        }

        var played = SessionPause.BillableElapsed(session, startedAt, nowUtc);

        if (string.Equals(session.BillingMode, BillingModeNames.PrepaidWallet, StringComparison.Ordinal)
            && Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
        {
            return await QuotePrepaidAsync(dbContext, session, playerAccountId, tariffVersionId, played, cancellationToken);
        }

        if (session.TariffRuleVersionId.StartsWith(PackageTariffPrefix, StringComparison.Ordinal)
            && Guid.TryParse(session.TariffRuleVersionId[PackageTariffPrefix.Length..], out var playerPackageId))
        {
            return await QuotePackageAsync(dbContext, session, playerAccountId, playerPackageId, played, cancellationToken);
        }

        // Постоплата и так выставляется по факту на закрытии, гостевой и комплиментарной
        // возвращать нечего.
        return PlayerEarlyEndQuote.Nothing("TJS");
    }

    /// <summary>
    /// Записи возврата по расчёту. Сохраняет вызывающий — тем же SaveChanges, что закрывает сессию:
    /// иначе был бы момент, когда сессия закрыта, а деньги и минуты не вернулись.
    /// </summary>
    public static void AppendEntries(
        PlatformDbContext dbContext,
        SessionEntity session,
        Guid playerAccountId,
        PlayerEarlyEndQuote quote,
        Guid actorId,
        DateTimeOffset nowUtc)
    {
        if (quote.Money.RefundMinorUnits > 0)
        {
            dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                session.OrganizationId,
                session.BranchId,
                playerAccountId,
                session.SessionId,
                playerPackageId: null,
                LedgerEntryTypeNames.Refund,
                LedgerAccountTypeNames.Wallet,
                quote.Money.RefundMinorUnits,
                quantitySeconds: 0,
                quote.CurrencyCode,
                LedgerEntryTypeNames.Refund,
                $"session:{session.SessionId:D}:early-end refund",
                quote.ChargeEntryId,
                actorId,
                nowUtc));
        }

        // Кешбэк разматывается вместе с деньгами. Иначе: оплатить восемь часов, встать через пять
        // минут, забрать деньги и оставить кешбэк за восемь.
        if (quote.Money.CashbackReversalMinorUnits > 0)
        {
            dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                session.OrganizationId,
                session.BranchId,
                playerAccountId,
                session.SessionId,
                playerPackageId: null,
                LedgerEntryTypeNames.Reversal,
                LedgerAccountTypeNames.Wallet,
                -quote.Money.CashbackReversalMinorUnits,
                quantitySeconds: 0,
                quote.CurrencyCode,
                LedgerEntryTypeNames.Reversal,
                $"session:{session.SessionId:D}:early-end cashback reversal",
                quote.CashbackEntryId,
                actorId,
                nowUtc));
        }

        if (quote.PlayerPackageId is { } playerPackageId)
        {
            AppendPackageReturn(dbContext, session, playerAccountId, playerPackageId, LedgerAccountTypeNames.PackageTime,
                quote.PackageIncludedSecondsReturned, quote.PackageIncludedEntryId, quote.CurrencyCode, actorId, nowUtc);
            AppendPackageReturn(dbContext, session, playerAccountId, playerPackageId, LedgerAccountTypeNames.BonusTime,
                quote.PackageBonusSecondsReturned, quote.PackageBonusEntryId, quote.CurrencyCode, actorId, nowUtc);
        }
    }

    private static async Task<PlayerEarlyEndQuote> QuotePrepaidAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        Guid playerAccountId,
        Guid tariffVersionId,
        TimeSpan played,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
            v => v.OrganizationId == session.OrganizationId
                 && v.BranchId == session.BranchId
                 && v.TariffVersionId == tariffVersionId,
            cancellationToken);
        if (version is null)
        {
            return PlayerEarlyEndQuote.Nothing("TJS");
        }

        var pricing = new TariffPricing(
            version.PricePerMinuteMinorUnits,
            version.MinimumBillableMinutes,
            version.RoundingIncrementMinutes,
            version.CurrencyCode);
        var actual = TariffBilling.ComputeForElapsed(played, pricing);

        // Начисления по этой сессии. Списания отрицательные, поэтому заряд берётся со знаком
        // минус; кешбэк положительный.
        var sessionEntries = await dbContext.LedgerEntries.AsNoTracking()
            .Where(entry => entry.SessionId == session.SessionId && entry.PlayerAccountId == playerAccountId)
            .Select(entry => new { entry.LedgerEntryId, entry.EntryType, entry.AmountMinorUnits })
            .ToListAsync(cancellationToken);

        var charged = -sessionEntries
            .Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge)
            .Sum(entry => entry.AmountMinorUnits);
        var cashback = sessionEntries
            .Where(entry => entry.EntryType == LedgerEntryTypeNames.Cashback)
            .Sum(entry => entry.AmountMinorUnits);

        return new PlayerEarlyEndQuote(
            actual?.BillableMinutes ?? 0,
            version.CurrencyCode,
            PrepaidEarlyEndSettlement.Compute(charged, actual?.AmountMinorUnits ?? charged, cashback),
            sessionEntries.FirstOrDefault(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge)?.LedgerEntryId,
            sessionEntries.FirstOrDefault(entry => entry.EntryType == LedgerEntryTypeNames.Cashback)?.LedgerEntryId,
            PlayerPackageId: null,
            0,
            0,
            null,
            null);
    }

    /// <summary>
    /// Пакет списывается вперёд на выбранное время. Раньше при раннем выходе остаток пропадал:
    /// взял три часа из пакета, ушёл через час — два часа пакета сгорели. Теперь сыгранное
    /// округляется вверх до минуты, а остальное возвращается туда, откуда списано: сначала
    /// основные минуты, потом бонусные.
    /// </summary>
    private static async Task<PlayerEarlyEndQuote> QuotePackageAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        Guid playerAccountId,
        Guid playerPackageId,
        TimeSpan played,
        CancellationToken cancellationToken)
    {
        var consumption = await dbContext.LedgerEntries.AsNoTracking()
            .Where(entry => entry.SessionId == session.SessionId
                && entry.PlayerAccountId == playerAccountId
                && entry.PlayerPackageId == playerPackageId
                && (entry.AccountType == LedgerAccountTypeNames.PackageTime
                    || entry.AccountType == LedgerAccountTypeNames.BonusTime))
            .Select(entry => new { entry.LedgerEntryId, entry.AccountType, entry.QuantitySeconds, entry.CurrencyCode })
            .ToListAsync(cancellationToken);

        var includedConsumed = -consumption
            .Where(entry => entry.AccountType == LedgerAccountTypeNames.PackageTime)
            .Sum(entry => entry.QuantitySeconds);
        var bonusConsumed = -consumption
            .Where(entry => entry.AccountType == LedgerAccountTypeNames.BonusTime)
            .Sum(entry => entry.QuantitySeconds);
        var playedMinutes = Math.Max(1, (int)Math.Ceiling(played.TotalMinutes));
        var toReturn = Math.Max(0, includedConsumed + bonusConsumed - playedMinutes * 60);
        var includedReturned = Math.Min(Math.Max(0, includedConsumed), toReturn);
        var bonusReturned = Math.Min(Math.Max(0, bonusConsumed), toReturn - includedReturned);

        return new PlayerEarlyEndQuote(
            playedMinutes,
            consumption.FirstOrDefault()?.CurrencyCode ?? "TJS",
            new PrepaidEarlyEndSettlement(0, 0),
            null,
            null,
            playerPackageId,
            includedReturned,
            bonusReturned,
            consumption.FirstOrDefault(entry => entry.AccountType == LedgerAccountTypeNames.PackageTime && entry.QuantitySeconds < 0)?.LedgerEntryId,
            consumption.FirstOrDefault(entry => entry.AccountType == LedgerAccountTypeNames.BonusTime && entry.QuantitySeconds < 0)?.LedgerEntryId);
    }

    private static void AppendPackageReturn(
        PlatformDbContext dbContext,
        SessionEntity session,
        Guid playerAccountId,
        Guid playerPackageId,
        string accountType,
        int seconds,
        Guid? reversesEntryId,
        string currencyCode,
        Guid actorId,
        DateTimeOffset nowUtc)
    {
        if (seconds <= 0)
        {
            return;
        }

        dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
            session.OrganizationId,
            session.BranchId,
            playerAccountId,
            session.SessionId,
            playerPackageId,
            LedgerEntryTypeNames.Refund,
            accountType,
            amountMinorUnits: 0,
            seconds,
            currencyCode,
            LedgerEntryTypeNames.Refund,
            $"session:{session.SessionId:D}:early-end package time return",
            reversesEntryId,
            actorId,
            nowUtc));
    }
}
