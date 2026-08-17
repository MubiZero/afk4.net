using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Расписание тарифа, прочитанное из базы.
///
/// Одно место на проект, где расписание встречается с часовым поясом филиала: окно владелец
/// задаёт по местному времени клуба, а хранится и сравнивается всё в UTC. Вторая реализация
/// этого перевода однажды разошлась бы с первой на переводе часов и продала бы утренний час по
/// вечерней цене.
/// </summary>
internal static class TariffAvailability
{
    /// <summary>
    /// Действует ли выбранная версия тарифа в этот момент. Неизвестная версия — не наш вопрос:
    /// её отсутствие обнаружит и назовёт расчёт, который идёт следом.
    /// </summary>
    public static Task<bool> AppliesAtAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        Guid tariffVersionId,
        DateTimeOffset instant,
        CancellationToken cancellationToken) =>
        AppliesThroughoutAsync(
            dbContext, organizationId, branchId, tariffVersionId, instant, instant, cancellationToken);

    /// <summary>
    /// Действует ли выбранная версия тарифа на протяжении всего промежутка. Конец, равный началу
    /// (или раньше него), означает «конец неизвестен» — проверяется только сам момент.
    /// </summary>
    public static async Task<bool> AppliesThroughoutAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        Guid tariffVersionId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken)
    {
        var schedule = await (
            from version in dbContext.TariffVersions.AsNoTracking()
            join tariff in dbContext.Tariffs.AsNoTracking() on version.TariffId equals tariff.TariffId
            where version.TariffVersionId == tariffVersionId &&
                  version.OrganizationId == organizationId &&
                  version.BranchId == branchId
            select new
            {
                tariff.AppliesOnDaysMask,
                tariff.AppliesFromMinuteOfDay,
                tariff.AppliesToMinuteOfDay
            }).SingleOrDefaultAsync(cancellationToken);

        if (schedule is null)
        {
            return true;
        }

        if (!TariffSchedule.IsRestricted(
            schedule.AppliesOnDaysMask, schedule.AppliesFromMinuteOfDay, schedule.AppliesToMinuteOfDay))
        {
            return true;
        }

        var timeZoneId = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == organizationId && branch.BranchId == branchId)
            .Select(branch => branch.PreferredTimeZone)
            .SingleOrDefaultAsync(cancellationToken);

        return TariffSchedule.AppliesThroughout(
            schedule.AppliesOnDaysMask,
            schedule.AppliesFromMinuteOfDay,
            schedule.AppliesToMinuteOfDay,
            startsAtUtc,
            endsAtUtc,
            BranchLocalTime.ResolveZone(timeZoneId ?? "UTC"));
    }
}
