using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Common;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
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
        LedgerEntryTypeNames.ReferralBonus
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

// Постраничный журнал ledger игрока (keyset, не offset). Зеркалит стратегию PlayerHistoryProjector:
// курсор кодирует (CreatedAtUtc DESC, LedgerEntryId DESC); WHERE-фильтр по timestamp в SQL/InMemory,
// затем точный tie-break (CreatedAtUtc, LedgerEntryId) в памяти (EF Core InMemory не транслирует
// Guid.CompareTo внутри LINQ Where). Проекция — через переиспользуемый LedgerBalanceProjector.ToDto.
public static class PlayerLedgerProjector
{
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

        // Битый/пустой курсор → false → первая страница (CursorToken.TryDecode не бросает).
        bool hasCursor = CursorToken.TryDecode(before, out var afterTs, out var afterId);

        if (hasCursor)
        {
            query = query.Where(entry => entry.CreatedAtUtc <= afterTs);
        }

        // Удвоенное окно при курсоре: даёт запас кандидатов на in-memory tie-break + pageSize+1 для hasMore.
        var windowSize = hasCursor ? (pageSize + 1) * 2 : pageSize + 1;
        var candidates = await query
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.LedgerEntryId)
            .Take(windowSize)
            .ToListAsync(cancellationToken);

        List<LedgerEntryEntity> entries;
        if (hasCursor)
        {
            entries = candidates
                .Where(entry =>
                    entry.CreatedAtUtc < afterTs ||
                    (entry.CreatedAtUtc == afterTs && entry.LedgerEntryId.CompareTo(afterId) < 0))
                .Take(pageSize + 1)
                .ToList();
        }
        else
        {
            entries = candidates.Take(pageSize + 1).ToList();
        }

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
