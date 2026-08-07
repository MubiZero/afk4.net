using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Пишет снимок состояния подписок за прошедшие сутки. Снимается ВЧЕРАШНИЙ день, а не сегодняшний:
/// сутки должны кончиться, прежде чем про них можно сказать что-то окончательное.
/// </summary>
public sealed class EfSubscriptionSnapshotRunner(PlatformDbContext dbContext, TimeProvider timeProvider)
    : ISubscriptionSnapshotRunner
{
    /// <summary>Насколько глубоко задание готово доснять пропущенные дни после долгого простоя.</summary>
    private const int MaxBackfillDays = 30;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastCompleteDay = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var earliest = lastCompleteDay.AddDays(-MaxBackfillDays);

        // Последняя снятая дата на организацию, а не фиксированное окно: организация, которую
        // видят впервые, не имеет прошлого, которое можно было бы реконструировать — ей снимаем
        // только вчера. А организация с историей досняется от своей последней даты, что и создаёт
        // «дыру» ровно по числу пропущенных суток простоя, а не выдуманные 30 дней вперёд.
        var lastSnapshotDates = await dbContext.SubscriptionDailySnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, LastDate = group.Max(snapshot => snapshot.SnapshotDate) })
            .ToDictionaryAsync(row => row.OrganizationId, row => row.LastDate, cancellationToken);

        var subscriptions = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var written = 0;
        var createdAt = timeProvider.GetUtcNow();

        foreach (var subscription in subscriptions)
        {
            var startDay = lastSnapshotDates.TryGetValue(subscription.OrganizationId, out var lastDate)
                ? lastDate.AddDays(1)
                : lastCompleteDay;
            // Глубина досъёмки после простоя ограничена MaxBackfillDays — иначе долгий простой
            // потащил бы историю глубже, чем разумно доверять реконструкции задним числом.
            if (startDay < earliest) startDay = earliest;

            for (var day = startDay; day <= lastCompleteDay; day = day.AddDays(1))
            {
                // Досняли пропущенный день — но состояние берём СЕГОДНЯШНЕЕ: восстановить, каким
                // оно было позавчера, нечем. Это честная цена простоя, а не точная реконструкция.
                dbContext.SubscriptionDailySnapshots.Add(new SubscriptionDailySnapshotEntity
                {
                    SubscriptionDailySnapshotId = Guid.NewGuid(),
                    OrganizationId = subscription.OrganizationId,
                    SnapshotDate = day,
                    Status = subscription.Status,
                    PlanCode = subscription.PlanCode,
                    MonthlyAmountMinorUnits = MonthlyAmount(subscription, createdAt),
                    CurrencyCode = subscription.CurrencyCode,
                    CreatedAtUtc = createdAt
                });
                written++;
            }
        }

        if (written > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return written;
    }

    private static long MonthlyAmount(OrganizationSubscriptionEntity subscription, DateTimeOffset now)
    {
        var gross = subscription.BillingInterval == BillingIntervalNames.Yearly
            ? subscription.AmountMinorUnits / 12
            : subscription.AmountMinorUnits;

        var discountApplies = subscription.DiscountUntilUtc is null || subscription.DiscountUntilUtc > now;
        if (!discountApplies) return gross;

        // Фиксированная скидка задана на период выставления; у годового плана её тоже надо
        // привести к месяцу, иначе месячная цена уедет в минус на порядок.
        var fixedDiscount = subscription.DiscountAmountMinorUnits is { } amount
            ? (subscription.BillingInterval == BillingIntervalNames.Yearly ? amount / 12 : amount)
            : (long?)null;

        // SubscriptionDiscount.Apply возвращает РАЗМЕР скидки (не итоговую сумму), поэтому
        // вычитаем результат из валовой суммы сами.
        return gross - SubscriptionDiscount.Apply(gross, subscription.DiscountPercent, fixedDiscount);
    }
}
