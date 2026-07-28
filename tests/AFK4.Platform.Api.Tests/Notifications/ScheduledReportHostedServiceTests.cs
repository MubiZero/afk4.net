using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class ScheduledReportHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_FirstTick_EnqueuesDueScheduledReport()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var yesterday = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        var now = DateTimeOffset.Parse("2026-06-02T06:00:00Z");
        var recorder = new RecordingNotificationService();

        var services = new ServiceCollection();
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IReportService, EfReportService>();
        services.AddScoped<IOrganizationOwnerResolver, EfOrganizationOwnerResolver>();
        services.AddScoped<INotificationService>(_ => recorder);
        services.AddScoped<IScheduledReportRunner, EfScheduledReportRunner>();
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var ownerId = Guid.NewGuid();
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId, Slug = "club", Name = "Demo Club", Status = "active",
                PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = now, UpdatedAtUtc = now
            });
            db.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = ownerId, OrganizationId = orgId, UserName = "owner", NormalizedUserName = "OWNER",
                DisplayName = "Club Owner", Email = "owner@club.example", PasswordHash = "x", IsActive = true, CreatedAtUtc = now
            });
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
                OrganizationId = orgId, BranchId = branchId, RoleName = OrganizationRoleNames.OrganizationOwner
            });
            db.ReportSchedules.Add(new ReportScheduleEntity
            {
                ReportScheduleId = scheduleId, OrganizationId = orgId, BranchId = branchId,
                ReportType = ScheduledReportTypeNames.Sales, Frequency = ReportScheduleFrequencyNames.Daily,
                IsActive = true, NextRunUtc = now.AddHours(-6), CreatedByStaffUserId = Guid.NewGuid(),
                CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddDays(-1)
            });
            db.PosSales.Add(new PosSaleEntity
            {
                PosSaleId = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId, ShiftId = Guid.NewGuid(),
                CreatedByStaffUserId = Guid.NewGuid(), State = PosSaleStateNames.Paid, CurrencyCode = "TJS",
                TotalMinorUnits = 5000, CreatedAtUtc = yesterday, PaidAtUtc = yesterday
            });
            await db.SaveChangesAsync();
        }

        var time = new FixedTimeProvider(now);
        var hosted = new ScheduledReportHostedService(
            provider, time,
            Options.Create(new NotificationOptions { ScheduledReportInterval = TimeSpan.FromHours(1) }),
            NullLogger<ScheduledReportHostedService>.Instance);

        using var cts = new CancellationTokenSource();
        await hosted.StartAsync(cts.Token);

        for (var attempt = 0; attempt < 200 && recorder.Requests.Count == 0; attempt++)
        {
            await Task.Delay(20, CancellationToken.None);
        }

        await cts.CancelAsync();
        await hosted.StopAsync(CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.ScheduledReport, request.TemplateKey);
        Assert.Equal($"report-scheduled:{scheduleId:N}:2026-06-01", request.IdempotencyKey);
        Assert.Single(request.Attachments!);
    }
}
