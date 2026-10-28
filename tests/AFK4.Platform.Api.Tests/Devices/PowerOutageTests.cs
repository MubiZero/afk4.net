using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// «Свет пропал — сессия не пропала» (docs/roadmap/power-outage-resume.md): когда в клубе отключали свет, гостям
/// возвращается время простоя; один выдернутый из розетки ПК времени не получает.
/// </summary>
public sealed class PowerOutageTests
{
    // Часы сервера настоящие: отключение отсчитывается назад от «сейчас», а ПК сообщают свой аптайм. Фабрика —
    // в теле теста, не в помощнике: она держит состояние в AsyncLocal, и из async-помощника оно не возвращается.

    [Fact]
    public async Task WhenTheClubLostPower_EveryInterruptedGuestGetsTheDowntimeBack_AndThePcOpens()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var outage = DateTimeOffset.UtcNow.AddMinutes(-12);
        var a = await EnrollWithSessionAsync(factory, client, "PC-01", outage, endsInMinutes: 50);
        var b = await EnrollWithSessionAsync(factory, client, "PC-02", outage, endsInMinutes: 50);

        // Обесточены с «12 минут назад» до «2 минуты назад» (аптайм 120 с): примерно 10 минут простоя.
        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);
        await HeartbeatAsync(client, b.Enrollment, powerLostFrom: outage.AddSeconds(20), uptimeSeconds: 120);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessionA = await db.Sessions.SingleAsync(row => row.SessionId == a.SessionId);
        AssertAbout(outage.AddMinutes(60), sessionA.EndsAtUtc);
        Assert.Equal(SessionStateNames.Active, sessionA.State);
        Assert.True(await db.DeviceCommands.AnyAsync(command => command.DeviceId == a.Enrollment.DeviceId && command.Type == DeviceCommandTypeNames.Unlock));
        AssertAbout(outage.AddMinutes(60).AddSeconds(-20), (await db.Sessions.SingleAsync(row => row.SessionId == b.SessionId)).EndsAtUtc);
    }

    // Один ПК, выдернутый из розетки, — это не отключение света: иначе «выдерни шнур» было бы бесплатной паузой.
    [Fact]
    public async Task OnePcLosingPowerAlone_GetsNoTimeBack()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var outage = DateTimeOffset.UtcNow.AddMinutes(-12);
        var a = await EnrollWithSessionAsync(factory, client, "PC-01", outage, endsInMinutes: 50);

        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(outage.AddMinutes(50), (await db.Sessions.SingleAsync(row => row.SessionId == a.SessionId)).EndsAtUtc);
    }

    // ПК на ИБП (часто — ПК администратора) сказал «работаю от батареи» в тот же момент: одного обесточенного хватает.
    [Fact]
    public async Task OnePcPlusAPcOnUpsBattery_IsAClubOutage()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        // ИБП сказал «от батареи» минуту назад, ПК обесточился тогда же и уже включился (аптайм 10 с).
        var outage = DateTimeOffset.UtcNow.AddMinutes(-1);
        var a = await EnrollWithSessionAsync(factory, client, "PC-01", outage, endsInMinutes: 50);
        var ups = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: "ADMIN", publicKey: "admin-key");
        await HeartbeatAsync(client, ups, onBattery: true);

        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 10);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True((await db.Sessions.SingleAsync(row => row.SessionId == a.SessionId)).EndsAtUtc > outage.AddMinutes(50));
    }

    // Час без света: гость, скорее всего, ушёл переждать. Время возвращено, сессия ждёт его на паузе «свет»,
    // и двадцатиминутное правило долгой паузы её не закрывает. Время вышло во время отключения — ПК был заперт
    // автозащитой; после возврата она снова запрёт его в новый конец.
    [Fact]
    public async Task ALongOutage_ReturnsTheTime_AndKeepsTheSessionPausedForTheGuest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var outage = DateTimeOffset.UtcNow.AddMinutes(-62);
        var a = await EnrollWithSessionAsync(factory, client, "PC-01", outage, endsInMinutes: 5, autoLocked: true);
        var b = await EnrollWithSessionAsync(factory, client, "PC-02", outage, endsInMinutes: 50);

        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);
        var beat = await HeartbeatAsync(client, b.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);
        Assert.Equal(DeviceLiveSessionHoldNames.PowerPaused, beat.LiveSession?.Hold);

        // Пауза стоит уже 45 минут — дольше обычных двадцати.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await setup.Sessions.SingleAsync(row => row.SessionId == a.SessionId)).PausedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-45);
            await setup.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<AutoProtectionRunner>().RunOnceAsync(CancellationToken.None);
        }

        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessionA = await db.Sessions.SingleAsync(row => row.SessionId == a.SessionId);
        Assert.Equal((SessionStateNames.Paused, SessionPauseReasonNames.Power), (sessionA.State, sessionA.PauseReason));
        AssertAbout(outage.AddMinutes(65), sessionA.EndsAtUtc);
        Assert.Null(sessionA.AutoLockedAtUtc);
    }

    [Fact]
    public async Task TheSameReportTwice_ReturnsTheTimeOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var outage = DateTimeOffset.UtcNow.AddMinutes(-12);
        var a = await EnrollWithSessionAsync(factory, client, "PC-01", outage, endsInMinutes: 50);
        var b = await EnrollWithSessionAsync(factory, client, "PC-02", outage, endsInMinutes: 50);

        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);
        await HeartbeatAsync(client, b.Enrollment, powerLostFrom: outage, uptimeSeconds: 120);
        await HeartbeatAsync(client, a.Enrollment, powerLostFrom: outage, uptimeSeconds: 125);
        await HeartbeatAsync(client, b.Enrollment, powerLostFrom: outage, uptimeSeconds: 125);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        AssertAbout(outage.AddMinutes(60), (await db.Sessions.SingleAsync(row => row.SessionId == a.SessionId)).EndsAtUtc);
    }

    // Сервер считает «включился» своими часами в момент сердцебиения: сравниваем с точностью до нескольких секунд.
    private static void AssertAbout(DateTimeOffset expected, DateTimeOffset? actual) =>
        Assert.InRange(actual!.Value, expected.AddSeconds(-5), expected.AddSeconds(5));

    private static async Task<(InstallEnrollResponse Enrollment, Guid SessionId)> EnrollWithSessionAsync(
        PlatformApiFactory factory, HttpClient client, string name, DateTimeOffset outage, int endsInMinutes, bool autoLocked = false)
    {
        var enrollment = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: name, publicKey: $"{name}-key");
        var sessionId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (!await db.Zones.AnyAsync(zone => zone.ZoneId == TestIds.ZoneId))
        {
            db.Zones.Add(new ZoneEntity { ZoneId = TestIds.ZoneId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Name = "Hall", SortOrder = 1, CreatedAtUtc = outage });
        }

        db.Seats.Add(new SeatEntity { SeatId = seatId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, ZoneId = TestIds.ZoneId, Name = name, SortOrder = 1, CreatedAtUtc = outage });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = seatId,
            DeviceId = enrollment.DeviceId,
            PlayerKind = "guest",
            TariffRuleVersionId = "tariff",
            BillingMode = "prepaid_cash",
            State = SessionStateNames.Active,
            RequestedAtUtc = outage.AddMinutes(-10),
            StartedAtUtc = outage.AddMinutes(-10),
            EndsAtUtc = outage.AddMinutes(endsInMinutes),
            AutoLockedAtUtc = autoLocked ? outage.AddMinutes(endsInMinutes) : null,
            UpdatedAtUtc = outage.AddMinutes(-10),
            Version = 1
        });
        await db.SaveChangesAsync();
        return (enrollment, sessionId);
    }

    private static async Task<DeviceHeartbeatResponse> HeartbeatAsync(
        HttpClient client, InstallEnrollResponse enrollment, DateTimeOffset? powerLostFrom = null, long? uptimeSeconds = null, bool? onBattery = null)
    {
        var request = new DeviceHeartbeatRequest(
            TestIds.OrganizationId, TestIds.BranchId, enrollment.DeviceId, "PC", "0.1.0", "0.1.0",
            DateTimeOffset.UtcNow, IsLocked: true, ActiveSessionId: null, ActiveSessionLeaseExpiresAtUtc: null, ActiveSessionLeaseSequence: null,
            PowerLostFromUtc: powerLostFrom, UptimeSeconds: uptimeSeconds, OnBatteryPower: onBattery);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{enrollment.DeviceId}/heartbeat") { Content = JsonContent.Create(request) };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>())!;
    }

}
