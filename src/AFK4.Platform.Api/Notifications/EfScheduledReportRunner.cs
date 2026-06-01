using System.Globalization;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

public sealed class EfScheduledReportRunner(
    PlatformDbContext dbContext,
    IReportService reportService,
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications) : IScheduledReportRunner
{
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await dbContext.ReportSchedules
            .Where(schedule => schedule.IsActive && schedule.NextRunUtc <= now)
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        foreach (var schedule in due)
        {
            if (await TryDeliverAsync(schedule, now, cancellationToken))
            {
                enqueued++;
            }

            schedule.LastRunUtc = now;
            schedule.NextRunUtc = AdvanceFrom(now, schedule.Frequency);
            schedule.UpdatedAtUtc = now;
        }

        if (due.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return enqueued;
    }

    private async Task<bool> TryDeliverAsync(ReportScheduleEntity schedule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (windowStart, windowEnd) = Window(now, schedule.Frequency);

        var csv = await GenerateCsvAsync(schedule, windowStart, windowEnd, cancellationToken);
        if (csv is null)
        {
            return false;
        }

        var recipient = await ownerResolver.ResolveAsync(schedule.OrganizationId, cancellationToken);
        if (recipient is null)
        {
            return false;
        }

        var windowDate = windowStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.ScheduledReport,
            Category: NotificationCategory.Digest,
            Recipient: new NotificationRecipient(
                Locale: string.Empty,
                EmailAddress: recipient.Email,
                StaffUserId: recipient.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = recipient.DisplayName,
                ["organizationName"] = recipient.OrganizationName,
                ["reportType"] = schedule.ReportType,
                ["periodStart"] = windowDate,
                ["periodEnd"] = windowEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
            IdempotencyKey: $"report-scheduled:{schedule.ReportScheduleId:N}:{windowDate}",
            OrganizationId: schedule.OrganizationId,
            BranchId: schedule.BranchId,
            Attachments:
            [
                new NotificationAttachment(
                    FileName: $"{schedule.ReportType}_{windowDate}.csv",
                    ContentType: "text/csv",
                    Content: Encoding.UTF8.GetBytes(csv)),
            ]);

        await notifications.SendAsync(request, cancellationToken);
        return true;
    }

    private async Task<string?> GenerateCsvAsync(
        ReportScheduleEntity schedule,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        var query = new ReportSearchQuery(windowStart, windowEnd, null);
        return schedule.ReportType switch
        {
            ScheduledReportTypeNames.Shifts =>
                ReportCsvExporter.ExportShiftReport(await reportService.GetShiftReportAsync(schedule.OrganizationId, schedule.BranchId, query, cancellationToken)),
            ScheduledReportTypeNames.Sales =>
                ReportCsvExporter.ExportSalesReport(await reportService.GetSalesReportAsync(schedule.OrganizationId, schedule.BranchId, query, cancellationToken)),
            ScheduledReportTypeNames.GameplayTime =>
                ReportCsvExporter.ExportGameplayTimeReport(await reportService.GetGameplayTimeReportAsync(schedule.OrganizationId, schedule.BranchId, query, cancellationToken)),
            ScheduledReportTypeNames.CashOperations =>
                ReportCsvExporter.ExportCashOperationReport(await reportService.GetCashOperationReportAsync(schedule.OrganizationId, schedule.BranchId, query, cancellationToken)),
            ScheduledReportTypeNames.OperatorActions =>
                ReportCsvExporter.ExportOperatorActionReport(await reportService.GetOperatorActionReportAsync(schedule.OrganizationId, schedule.BranchId, query, cancellationToken)),
            _ => null,
        };
    }

    private static DateTimeOffset DayStart(DateTimeOffset now) =>
        new(DateOnly.FromDateTime(now.UtcDateTime).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static (DateTimeOffset Start, DateTimeOffset End) Window(DateTimeOffset now, string frequency)
    {
        var end = DayStart(now);
        var start = frequency switch
        {
            ReportScheduleFrequencyNames.Weekly => end.AddDays(-7),
            ReportScheduleFrequencyNames.Monthly => end.AddMonths(-1),
            _ => end.AddDays(-1),
        };
        return (start, end);
    }

    private static DateTimeOffset AdvanceFrom(DateTimeOffset now, string frequency)
    {
        var end = DayStart(now);
        return frequency switch
        {
            ReportScheduleFrequencyNames.Weekly => end.AddDays(7),
            ReportScheduleFrequencyNames.Monthly => end.AddMonths(1),
            _ => end.AddDays(1),
        };
    }
}
