using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>
/// Что приходит в Telegram тем, кто подключил «Отчёты в Telegram»: итоги каждой закрытой смены,
/// тревога о расхождении кассы и утренняя сводка за вчера. Получатели — сотрудники этого филиала с
/// правом смотреть отчёты: управляющий одного зала не получает чужую выручку.
/// </summary>
public sealed class TelegramReports(
    PlatformDbContext db,
    INotificationService notifications,
    IReportService reports,
    OrganizationAdminReportService summaries)
{
    /// <summary>С какого часа по времени клуба уходит сводка за вчера: к утру, а не в полночь.</summary>
    public const int DigestLocalHour = 9;

    private static readonly string[] ReportRoles =
        new[]
        {
            OrganizationRoleNames.OrganizationOwner, OrganizationRoleNames.BranchManager, OrganizationRoleNames.ShiftSupervisor,
            OrganizationRoleNames.Operator, OrganizationRoleNames.Technician, OrganizationRoleNames.Accountant
        }
        .Where(role => OrganizationPermissionCatalog.GetPermissions([role]).Contains(OrganizationPermissionNames.ViewReports))
        .ToArray();

    public async Task ShiftClosedAsync(ShiftEntity shift, CancellationToken cancellationToken)
    {
        var recipients = await RecipientsAsync(shift.OrganizationId, shift.BranchId, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var branch = await db.Branches.AsNoTracking().SingleAsync(row => row.BranchId == shift.BranchId, cancellationToken);
        var revenue = await reports.GetShiftRevenueAsync(shift.OrganizationId, shift.BranchId, shift.ShiftId, cancellationToken);
        if (revenue is null)
        {
            return;
        }

        var closedBy = shift.ClosedByStaffUserId is { } closedById
            ? await db.StaffUsers.AsNoTracking().Where(row => row.StaffUserId == closedById).Select(row => row.DisplayName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var zone = BranchLocalTime.ResolveZone(branch.PreferredTimeZone);
        var locale = branch.PreferredLocale;
        var currency = shift.CurrencyCode;

        foreach (var recipient in recipients)
        {
            await notifications.SendAsync(new NotificationRequest(
                TemplateKey: NotificationTemplateKeys.ShiftClosed,
                Category: NotificationCategory.Operational,
                Recipient: Recipient(recipient, locale),
                Tokens: new Dictionary<string, string>
                {
                    ["branchName"] = branch.Name,
                    ["closedBy"] = closedBy ?? "—",
                    ["period"] = $"{LocalTime(shift.OpenedAtUtc, zone)} – {LocalTime(shift.ClosedAtUtc ?? shift.OpenedAtUtc, zone)}",
                    ["earnedTotal"] = ReportMoney.Format(revenue.Earned.Total.MinorUnits, currency, locale),
                    ["earnedTime"] = ReportMoney.Format(revenue.Earned.Time.MinorUnits, currency, locale),
                    ["earnedGoods"] = ReportMoney.Format(revenue.Earned.Goods.MinorUnits, currency, locale),
                    ["cashIn"] = ReportMoney.Format(revenue.Inflow.Cash.MinorUnits, currency, locale),
                    ["nonCashIn"] = ReportMoney.Format(revenue.Inflow.NonCash.MinorUnits, currency, locale),
                    ["expectedCash"] = ReportMoney.Format(shift.ExpectedCashMinorUnits, currency, locale),
                    ["countedCash"] = ReportMoney.Format(shift.CountedCashMinorUnits, currency, locale),
                    ["difference"] = ReportMoney.Format(shift.DifferenceMinorUnits, currency, locale, signed: true),
                },
                IdempotencyKey: $"shift-closed:{shift.ShiftId:N}:{recipient.StaffUserId:N}",
                PreferredChannels: [NotificationChannel.Telegram],
                OrganizationId: shift.OrganizationId,
                BranchId: shift.BranchId), cancellationToken);
        }
    }

    /// <summary>Тревога о расхождении — тем же, кто получает итоги смены, сразу после них.</summary>
    public async Task DiscrepancyAsync(ShiftEntity shift, string organizationName, CancellationToken cancellationToken)
    {
        var recipients = await RecipientsAsync(shift.OrganizationId, shift.BranchId, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var branch = await db.Branches.AsNoTracking().SingleAsync(row => row.BranchId == shift.BranchId, cancellationToken);
        var zone = BranchLocalTime.ResolveZone(branch.PreferredTimeZone);
        foreach (var recipient in recipients)
        {
            var locale = branch.PreferredLocale;
            await notifications.SendAsync(new NotificationRequest(
                TemplateKey: NotificationTemplateKeys.ShiftDiscrepancy,
                Category: NotificationCategory.Operational,
                Recipient: Recipient(recipient, locale),
                Tokens: new Dictionary<string, string>
                {
                    ["displayName"] = recipient.DisplayName,
                    ["organizationName"] = organizationName,
                    ["branchName"] = branch.Name,
                    ["difference"] = ReportMoney.Format(shift.DifferenceMinorUnits, shift.CurrencyCode, locale, signed: true),
                    ["countedCash"] = ReportMoney.Format(shift.CountedCashMinorUnits, shift.CurrencyCode, locale),
                    ["expectedCash"] = ReportMoney.Format(shift.ExpectedCashMinorUnits, shift.CurrencyCode, locale),
                    // Суммы уже со знаком валюты: в шаблоне письма валюта стоит отдельным токеном.
                    ["currency"] = string.Empty,
                    ["closedDate"] = LocalTime(shift.ClosedAtUtc ?? shift.OpenedAtUtc, zone),
                },
                IdempotencyKey: $"shift-discrepancy:{shift.ShiftId:N}:{recipient.StaffUserId:N}",
                PreferredChannels: [NotificationChannel.Telegram],
                OrganizationId: shift.OrganizationId,
                BranchId: shift.BranchId), cancellationToken);
        }
    }

    /// <summary>
    /// Сводка за вчера по каждому филиалу, где кто-то подключил Telegram. Выручка — по тому же правилу,
    /// что «Сводка» в Панели (<see cref="OrganizationAdminReportService"/>): владелец не должен видеть две
    /// разные цифры за один день. Задание тикает раз в час; ключ повтора не даёт отправить дважды.
    /// </summary>
    public async Task<int> DailyDigestAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var branchIds = await (
            from assignment in db.StaffRoleAssignments
            join staff in db.StaffUsers on assignment.StaffUserId equals staff.StaffUserId
            where staff.IsActive && staff.TelegramChatId != null && ReportRoles.Contains(assignment.RoleName)
            select assignment.BranchId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var branch in await db.Branches.AsNoTracking().Where(row => branchIds.Contains(row.BranchId)).ToListAsync(cancellationToken))
        {
            var zone = BranchLocalTime.ResolveZone(branch.PreferredTimeZone);
            var localNow = TimeZoneInfo.ConvertTime(now, zone);
            if (localNow.Hour < DigestLocalHour)
            {
                continue;
            }

            var day = DateOnly.FromDateTime(localNow.DateTime).AddDays(-1);
            var summary = await summaries.GetSummaryAsync(branch.OrganizationId, branch.BranchId, day, day, cancellationToken);
            var currency = summary.Figures.NetRevenue.CurrencyCode;
            var locale = branch.PreferredLocale;
            foreach (var recipient in await RecipientsAsync(branch.OrganizationId, branch.BranchId, cancellationToken))
            {
                await notifications.SendAsync(new NotificationRequest(
                    TemplateKey: NotificationTemplateKeys.BranchDailySummary,
                    Category: NotificationCategory.Digest,
                    Recipient: Recipient(recipient, locale),
                    Tokens: new Dictionary<string, string>
                    {
                        ["branchName"] = branch.Name,
                        ["date"] = day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                        ["revenue"] = ReportMoney.Format(summary.Figures.NetRevenue.MinorUnits, currency, locale),
                        ["gameplay"] = ReportMoney.Format(summary.Figures.GameplayRevenue.MinorUnits, currency, locale),
                        ["goods"] = ReportMoney.Format(summary.Figures.PosNetSales.MinorUnits, currency, locale),
                        ["playedHours"] = (summary.Figures.GameplaySeconds / 3600m).ToString("0.#", ReportMoney.Culture(locale)),
                        ["attentionCount"] = summary.AttentionTotalCount.ToString(CultureInfo.InvariantCulture),
                    },
                    IdempotencyKey: $"branch-daily:{branch.BranchId:N}:{day:yyyy-MM-dd}:{recipient.StaffUserId:N}",
                    PreferredChannels: [NotificationChannel.Telegram],
                    OrganizationId: branch.OrganizationId,
                    BranchId: branch.BranchId), cancellationToken);
                sent++;
            }
        }

        return sent;
    }

    private async Task<IReadOnlyList<StaffUserEntity>> RecipientsAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken) =>
        await (
            from assignment in db.StaffRoleAssignments
            join staff in db.StaffUsers on assignment.StaffUserId equals staff.StaffUserId
            where assignment.OrganizationId == organizationId
                && assignment.BranchId == branchId
                && ReportRoles.Contains(assignment.RoleName)
                && staff.IsActive
                && staff.TelegramChatId != null
            select staff)
            .Distinct()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private static NotificationRecipient Recipient(StaffUserEntity staff, string locale) => new(
        Locale: locale,
        StaffUserId: staff.StaffUserId,
        TelegramChatId: staff.TelegramChatId!.Value.ToString(CultureInfo.InvariantCulture));

    private static string LocalTime(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
}
