using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Notifications.Telegram;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// Что приходит в Telegram: итоги каждой закрытой смены и сводка филиала за вчера — тем сотрудникам
/// этого филиала, кто видит отчёты и подключил Telegram. Кассир отчёты о выручке не получает.
/// </summary>
public sealed class TelegramReportsTests
{
    [Fact]
    public async Task AClosedShift_SendsItsResults_ToEveryoneWithReportsWhoLinkedTelegram()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTeamAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var shifts = scope.ServiceProvider.GetRequiredService<EfShiftService>();
            var opened = await shifts.OpenShiftAsync(TestIds.BranchId, TestIds.TechnicianStaffUserId,
                new OpenShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "front", "open-1"), CancellationToken.None);
            var closed = await shifts.CloseShiftAsync(opened.Response!.ShiftId, TestIds.TechnicianStaffUserId,
                new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "ok", "close-1"), CancellationToken.None);
            Assert.True(closed.Succeeded, closed.Error);
        }

        var rows = await TelegramRowsAsync(factory, NotificationTemplateKeys.ShiftClosed);
        Assert.Equal(["101", "102"], rows.Select(row => row.RecipientAddress).Order());
        Assert.All(rows, row => Assert.Contains("Заработано", row.BodyHtml, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDailySummary_ArrivesInTheMorning_OncePerPerson()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTeamAsync(factory);

        // 08:00 и 10:00 в Душанбе (UTC+5): до девяти утра сводка ещё не уходит.
        Assert.Equal(0, await DigestAsync(factory, DateTimeOffset.Parse("2026-10-27T03:00:00Z")));
        Assert.Equal(2, await DigestAsync(factory, DateTimeOffset.Parse("2026-10-27T05:00:00Z")));
        await DigestAsync(factory, DateTimeOffset.Parse("2026-10-27T06:00:00Z"));

        var rows = await TelegramRowsAsync(factory, NotificationTemplateKeys.BranchDailySummary);
        Assert.Equal(["101", "102"], rows.Select(row => row.RecipientAddress).Order());
        Assert.All(rows, row => Assert.Contains("26.10.2026", row.Subject, StringComparison.Ordinal));
    }

    [Fact]
    public void Money_ReadsLikeInThePanel()
    {
        Assert.Equal("1 200,5 с.", ReportMoney.Format(120050, "TJS", "tg"));
        Assert.Equal("1,200.5 с.", ReportMoney.Format(120050, "TJS", "en"));
        Assert.Equal("−50 с.", ReportMoney.Format(-5000, "TJS", "ru", signed: true));
        Assert.Equal("+50 с.", ReportMoney.Format(5000, "TJS", "ru", signed: true));
        Assert.Equal("0 с.", ReportMoney.Format(0, "TJS", "ru", signed: true));
    }

    // Владелец (подключил, 101), управляющий (подключил, 102), бухгалтер (не подключил) и кассир
    // (подключил, но отчётов не видит, 104).
    private static async Task SeedTeamAsync(PlatformApiFactory factory)
    {
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.StaffUsers.SingleAsync(row => row.StaffUserId == TestIds.TechnicianStaffUserId)).TelegramChatId = 101;
        AddStaff(db, OrganizationRoleNames.BranchManager, 102);
        AddStaff(db, OrganizationRoleNames.Accountant, null);
        AddStaff(db, OrganizationRoleNames.Operator, 104);
        await db.SaveChangesAsync();
    }

    private static void AddStaff(PlatformDbContext db, string role, long? chatId)
    {
        var staffUserId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = role,
            NormalizedUserName = role.ToUpperInvariant(),
            DisplayName = role,
            PasswordHash = "x",
            TelegramChatId = chatId
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = role
        });
    }

    private static async Task<int> DigestAsync(PlatformApiFactory factory, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramReports>().DailyDigestAsync(now, CancellationToken.None);
    }

    private static async Task<List<NotificationOutboxEntity>> TelegramRowsAsync(PlatformApiFactory factory, string templateKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().NotificationOutbox
            .Where(row => row.TemplateKey == templateKey && row.Channel == nameof(NotificationChannel.Telegram))
            .ToListAsync();
    }
}
