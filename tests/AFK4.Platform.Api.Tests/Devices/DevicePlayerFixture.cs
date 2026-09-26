using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Клуб с одним заведённым ПК на месте «ПК 07» и игроком с ПИН-кодом — то, с чего начинается
/// любой вход на машине: номером и ПИН-кодом у самого ПК или по QR с телефона. Часы двигает тест.
/// </summary>
internal sealed class DevicePlayerFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-24T18:00:00Z");

    private DevicePlayerFixture(PlatformApiFactory factory, HttpClient client, MovableTimeProvider clock)
    {
        Factory = factory;
        Client = client;
        Clock = clock;
    }

    public PlatformApiFactory Factory { get; }

    public HttpClient Client { get; }

    public MovableTimeProvider Clock { get; }

    public DeviceEnrollmentResponse Device { get; private set; } = null!;

    public Guid PlayerAccountId { get; private set; }

    public string Phone { get; private set; } = string.Empty;

    public string Pin => PlayerPinTestData.DefaultPin;

    private Guid ZoneId { get; } = Guid.NewGuid();

    /// <summary>
    /// Синхронно и в самом тесте: база теста живёт в AsyncLocal, который фабрика ставит в
    /// конструкторе, а значение, поставленное внутри async-помощника, наверх не возвращается —
    /// тест ходил бы в чужую пустую базу.
    /// </summary>
    public static DevicePlayerFixture Create(Action<IServiceCollection>? extraServices = null)
    {
        var clock = new MovableTimeProvider(Start);
        var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            extraServices?.Invoke(services);
        });
        return new DevicePlayerFixture(factory, factory.CreateClient(), clock);
    }

    public async Task SeedAsync()
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(Factory, Client, OrganizationRoleNames.OrganizationOwner);
        Device = await EnrollDeviceAsync();
        await AssignSeatAsync(await AddSeatAsync("ПК 07"));
        var player = await AddPlayerAsync();
        PlayerAccountId = player.PlayerAccountId;
        Phone = player.Phone;
    }

    public Task<HttpResponseMessage> SignInAsync(string pin, string? phone = null, string? credential = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{Device.DeviceId}/player-sign-in")
        {
            Content = JsonContent.Create(new DevicePlayerSignInRequest(
                Device.OrganizationId, Device.BranchId, Device.DeviceId, phone ?? Phone, pin))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credential ?? Device.CredentialSecret);
        return Client.SendAsync(message);
    }

    public async Task<DeviceHeartbeatResponse> HeartbeatAsync(
        Guid? activeSessionId = null,
        DeviceEnrollmentResponse? device = null,
        string? macAddress = null,
        string? subnet = null)
    {
        device ??= Device;
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{device.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(new DeviceHeartbeatRequest(
                device.OrganizationId,
                device.BranchId,
                device.DeviceId,
                MachineName: "PC-007",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                ObservedAtUtc: Clock.GetUtcNow(),
                IsLocked: activeSessionId is null,
                ActiveSessionId: activeSessionId,
                ActiveSessionLeaseExpiresAtUtc: null,
                ActiveSessionLeaseSequence: null,
                NetworkMacAddress: macAddress,
                NetworkSubnet: subnet,
                NetworkBroadcastAddress: subnet is null ? null : "192.168.1.255"))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, device.CredentialSecret);
        var response = await Client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>())!;
    }

    /// <summary>Команда администратора этому ПК — тем же маршрутом, что у Панели.</summary>
    public Task<HttpResponseMessage> CommandAsync(string type, Dictionary<string, string>? payload = null, Guid? deviceId = null) =>
        Client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{(deviceId ?? Device.DeviceId):D}/commands",
            new CreateDeviceCommandRequest(type, payload ?? []));

    /// <summary>
    /// Живые токены ПК обоих видов. Доступ живёт 15 минут и истекает сам, а обновление старый
    /// доступ не гасит — поэтому считать их поровну нельзя; вход жив, пока жив хоть один.
    /// </summary>
    public async Task<IReadOnlyList<LiveToken>> LiveTokensAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = Clock.GetUtcNow();
        var refresh = await db.PlatformPersonRefreshTokens.AsNoTracking()
            .Where(token => token.DeviceId == Device.DeviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .Select(token => new LiveToken(token.PlatformPersonId, token.DeviceId, token.DeviceSignedInAtUtc))
            .ToListAsync();
        var access = await db.PlatformPersonAccessTokens.AsNoTracking()
            .Where(token => token.DeviceId == Device.DeviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .Select(token => new LiveToken(token.PlatformPersonId, token.DeviceId, token.DeviceSignedInAtUtc))
            .ToListAsync();
        return [.. refresh, .. access];
    }

    public async Task<Guid> StartSessionAsync(Guid? playerAccountId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seatId = await db.DeviceSeatAssignments
            .Where(assignment => assignment.DeviceId == Device.DeviceId && assignment.DetachedAtUtc == null)
            .Select(assignment => assignment.SeatId)
            .SingleAsync();
        var now = Clock.GetUtcNow();
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId,
            OrganizationId = Device.OrganizationId,
            BranchId = Device.BranchId,
            SeatId = seatId,
            DeviceId = Device.DeviceId,
            PlayerAccountId = playerAccountId,
            TariffRuleVersionId = "test",
            State = SessionStateNames.Active,
            RequestedAtUtc = now,
            StartedAtUtc = now,
            EndsAtUtc = now.AddHours(3),
            UpdatedAtUtc = now,
            Version = 1
        });
        await db.SaveChangesAsync();
        return sessionId;
    }

    public async Task EndSessionAsync(Guid sessionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(candidate => candidate.SessionId == sessionId);
        session.State = SessionStateNames.Ended;
        session.EndedAtUtc = Clock.GetUtcNow();
        await db.SaveChangesAsync();
    }

    public async Task<(Guid PlayerAccountId, Guid PlatformPersonId, string Phone)> AddPlayerAsync()
    {
        var phone = TestPhones.Next();
        var playerAccountId = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = playerAccountId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Игрок",
                PhoneNumber = phone,
                PreferredLocale = "ru",
                IsActive = true,
                CreatedAtUtc = Start
            });
            await db.SaveChangesAsync();
        }

        var personId = await PlayerPinTestData.AttachPersonWithPinAsync(Factory, playerAccountId);
        return (playerAccountId, personId, phone);
    }

    public async Task<Guid> AddSeatAsync(string name)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (!await db.Zones.AnyAsync(zone => zone.ZoneId == ZoneId))
        {
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = ZoneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Общий зал",
                SortOrder = 1,
                CreatedAtUtc = Start
            });
        }

        var seatId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = ZoneId,
            Name = name,
            SortOrder = 1,
            CreatedAtUtc = Start
        });
        await db.SaveChangesAsync();
        return seatId;
    }

    private async Task AssignSeatAsync(Guid seatId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            DeviceId = Device.DeviceId,
            SeatId = seatId,
            AttachedAtUtc = Start
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Ещё один ПК того же клуба — чужой для заявки, заведённой у первого.</summary>
    public Task<DeviceEnrollmentResponse> EnrollAnotherDeviceAsync() => EnrollDeviceAsync();

    private async Task<DeviceEnrollmentResponse> EnrollDeviceAsync()
    {
        var codeResponse = await Client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        Assert.True(codeResponse.IsSuccessStatusCode, await codeResponse.Content.ReadAsStringAsync());
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();

        var enrollmentResponse = await Client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code!.Code,
                MachineName: "PC-007",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: Start));
        Assert.True(enrollmentResponse.IsSuccessStatusCode, await enrollmentResponse.Content.ReadAsStringAsync());
        return (await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>())!;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>Клиент телефона игрока: входит публичным входом, как приложение.</summary>
    public async Task<HttpClient> PhoneClientAsync(string? phone = null)
    {
        var client = Factory.CreateClient();
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(TestIds.OrganizationId, phone ?? Phone, Pin));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = await signIn.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }
}

internal sealed record LiveToken(Guid PlatformPersonId, Guid? DeviceId, DateTimeOffset? DeviceSignedInAtUtc);
