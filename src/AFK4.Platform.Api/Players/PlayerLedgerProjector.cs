using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Common;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Валидация и нормализация query-параметров журнала ledger. Вынесена из эндпоинт-лямбды,
// чтобы юнит-тестировать без интеграционного харнеса (его в проекте нет — Global Constraints).
public static class PlayerLedgerFilter
{
    public const int DefaultLimit = 50;
    public const int MinLimit = 1;
    public const int MaxLimit = 100;

    private static readonly HashSet<string> KnownEntryTypes = new(StringComparer.Ordinal)
    {
        LedgerEntryTypeNames.TopUp,
        LedgerEntryTypeNames.GameplayCharge,
        LedgerEntryTypeNames.PackagePurchase,
        LedgerEntryTypeNames.PackageConsumption,
        LedgerEntryTypeNames.BonusGrant,
        LedgerEntryTypeNames.BonusConsumption,
        LedgerEntryTypeNames.Refund,
        LedgerEntryTypeNames.ManualCorrection,
        LedgerEntryTypeNames.PostpaidDebt,
        LedgerEntryTypeNames.DebtPayment,
        LedgerEntryTypeNames.WalletPayment,
        LedgerEntryTypeNames.Reversal,
        LedgerEntryTypeNames.Cashback,
        LedgerEntryTypeNames.ReferralBonus,
        LedgerEntryTypeNames.ReservationHold,
        LedgerEntryTypeNames.ReservationNoShowFee
    };

    private static readonly HashSet<string> KnownAccountTypes = new(StringComparer.Ordinal)
    {
        LedgerAccountTypeNames.Wallet,
        LedgerAccountTypeNames.Debt,
        LedgerAccountTypeNames.PackageTime,
        LedgerAccountTypeNames.BonusTime
    };

    // null дефолтит к 50; иначе зажимаем в [1, 100].
    public static int ClampLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        return Math.Clamp(limit.Value, MinLimit, MaxLimit);
    }

    // Пустой/null фильтр = «нет фильтра» (валиден). Непустой — должен быть из известных значений.
    public static bool IsValidEntryType(string? entryType) =>
        string.IsNullOrEmpty(entryType) || KnownEntryTypes.Contains(entryType);

    public static bool IsValidAccountType(string? accountType) =>
        string.IsNullOrEmpty(accountType) || KnownAccountTypes.Contains(accountType);
}

// Постраничный журнал ledger игрока (keyset, не offset): курсор кодирует (CreatedAtUtc DESC,
// LedgerEntryId DESC), нарезку делает KeysetPage. Проекция — через LedgerBalanceProjector.ToDto.
public static class PlayerLedgerProjector
{
    /// <summary>
    /// Выписка глазами игрока: все записи журнала, но без внутренних полей.
    ///
    /// Удержание под бронь и его снятие здесь есть, хотя раньше прятались как «событие без итога».
    /// Прятать их значило оставить остаток кошелька, который не складывается из видимых строк: в
    /// баланс удержание входит, а в выписку нет. Теперь строка снятия несёт повод, а каждая строка
    /// кошелька — остаток после неё.
    /// </summary>
    public static async Task<CursorPage<PlayerLedgerEntryDto>> GetPlayerLedgerPageAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? before,
        int? limit,
        CancellationToken cancellationToken)
    {
        var pageSize = PlayerLedgerFilter.ClampLimit(limit);

        var query = dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId);

        var entries = await KeysetPage.TakeAsync(
            query, entry => entry.CreatedAtUtc, entry => entry.LedgerEntryId, before, pageSize + 1, cancellationToken);

        var hasMore = entries.Count > pageSize;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        // Строка выписки объясняется чеком того визита, к которому привязана. Спрашиваем только
        // про сессии этой страницы и только те, по которым чек действительно выбит: ссылка,
        // ведущая в «чека нет», хуже её отсутствия.
        var sessionIds = entries
            .Where(entry => entry.SessionId != null)
            .Select(entry => entry.SessionId!.Value)
            .Distinct()
            .ToList();

        var sessionsWithReceipt = sessionIds.Count == 0
            ? []
            : (await dbContext.Receipts
                .AsNoTracking()
                .Where(receipt => receipt.SessionId != null && sessionIds.Contains(receipt.SessionId.Value))
                .Select(receipt => receipt.SessionId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        // Повод есть только у снятия удержания, поэтому и спрашиваем только про записи, которые
        // что-то отменяют: какие из отменённых были удержаниями.
        var reversedIds = entries
            .Where(entry => entry.ReversesLedgerEntryId != null)
            .Select(entry => entry.ReversesLedgerEntryId!.Value)
            .Distinct()
            .ToList();

        var reversedHolds = reversedIds.Count == 0
            ? []
            : (await dbContext.LedgerEntries
                .AsNoTracking()
                .Where(entry => reversedIds.Contains(entry.LedgerEntryId)
                    && entry.EntryType == LedgerEntryTypeNames.ReservationHold)
                .Select(entry => entry.LedgerEntryId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var balanceAfter = entries.Count == 0
            ? 0
            : await WalletBalanceAfterAsync(dbContext, playerAccountId, entries[0], cancellationToken);

        var items = new List<PlayerLedgerEntryDto>(entries.Count);
        foreach (var entry in entries)
        {
            var isWallet = entry.AccountType == LedgerAccountTypeNames.Wallet;
            items.Add(new PlayerLedgerEntryDto(
                entry.LedgerEntryId,
                entry.EntryType,
                new MoneyDto(entry.CurrencyCode, entry.AmountMinorUnits),
                entry.QuantitySeconds,
                entry.CreatedAtUtc,
                entry.SessionId is { } sessionId && sessionsWithReceipt.Contains(sessionId)
                    ? sessionId
                    : null,
                entry.ReversesLedgerEntryId is { } reversed && reversedHolds.Contains(reversed)
                    ? ReservationHold.TryReadReleaseCause(entry.Reason)
                    : null,
                isWallet ? new MoneyDto(entry.CurrencyCode, balanceAfter) : null));

            // Строки идут от новой к старой: остаток перед этой строкой — это остаток после
            // следующей, более старой.
            if (isWallet)
            {
                balanceAfter -= entry.AmountMinorUnits;
            }
        }

        var nextCursor = hasMore && entries.Count > 0
            ? CursorToken.Encode(entries[^1].CreatedAtUtc, entries[^1].LedgerEntryId)
            : null;

        return new CursorPage<PlayerLedgerEntryDto>(items, nextCursor);
    }

    /// <summary>
    /// Остаток кошелька сразу после <paramref name="newest"/> — самой новой строки страницы: весь
    /// кошелёк минус то, что случилось позже неё. Позже — это раньше в том же порядке, в каком
    /// режется выписка, поэтому строки с тем же моментом упорядочиваются базой, а не в памяти:
    /// порядок идентификаторов у базы и у .NET разный, и остаток разошёлся бы со строками.
    /// </summary>
    private static async Task<long> WalletBalanceAfterAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        LedgerEntryEntity newest,
        CancellationToken cancellationToken)
    {
        var wallet = dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId
                && entry.AccountType == LedgerAccountTypeNames.Wallet);

        var total = await wallet.SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
        var later = await wallet
            .Where(entry => entry.CreatedAtUtc > newest.CreatedAtUtc)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;

        // Все строки этого момента, а не только кошелёк: сама newest может быть не про кошелёк, и
        // без неё в списке не нашлось бы, где остановиться.
        var sameMoment = await dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId
                && entry.CreatedAtUtc == newest.CreatedAtUtc)
            .OrderByDescending(entry => entry.LedgerEntryId)
            .Select(entry => new { entry.LedgerEntryId, entry.AccountType, entry.AmountMinorUnits })
            .ToListAsync(cancellationToken);
        var laterAtSameMoment = sameMoment
            .TakeWhile(entry => entry.LedgerEntryId != newest.LedgerEntryId)
            .Where(entry => entry.AccountType == LedgerAccountTypeNames.Wallet)
            .Sum(entry => entry.AmountMinorUnits);

        return total - later - laterAtSameMoment;
    }

    public static async Task<CursorPage<LedgerEntryDto>> GetLedgerPageAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? entryType,
        string? accountType,
        string? before,
        int limit,
        CancellationToken cancellationToken)
    {
        var pageSize = PlayerLedgerFilter.ClampLimit(limit);

        var query = dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId);

        if (!string.IsNullOrEmpty(entryType))
        {
            query = query.Where(entry => entry.EntryType == entryType);
        }

        if (!string.IsNullOrEmpty(accountType))
        {
            query = query.Where(entry => entry.AccountType == accountType);
        }

        // Битый/пустой курсор → первая страница (CursorToken.TryDecode не бросает).
        var entries = await KeysetPage.TakeAsync(
            query, entry => entry.CreatedAtUtc, entry => entry.LedgerEntryId, before, pageSize + 1, cancellationToken);

        var hasMore = entries.Count > pageSize;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        var items = entries.Select(LedgerBalanceProjector.ToDto).ToList();

        string? nextCursor = hasMore && entries.Count > 0
            ? CursorToken.Encode(entries[^1].CreatedAtUtc, entries[^1].LedgerEntryId)
            : null;

        return new CursorPage<LedgerEntryDto>(items, nextCursor);
    }
}
