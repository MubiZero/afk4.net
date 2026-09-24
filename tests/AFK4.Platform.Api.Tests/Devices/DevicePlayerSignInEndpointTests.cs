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
/// Вход игрока на самом ПК (спека оболочки, §5.2–5.3). Главное здесь — не удобство, а граница:
/// токены, выданные на машине клуба, гаснут, когда за ней больше некому сидеть, и гасит их
/// сервер, а не хост, который может упасть или оказаться подменённым.
/// </summary>
public sealed class DevicePlayerSignInEndpointTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-24T18:00:00Z");

    [Fact]
    public async Task SignIn_WithTheRightPin_IssuesTokensBoundToThisPc()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();

        var response = await fixture.SignInAsync(fixture.Pin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        Assert.Equal(fixture.PlayerAccountId, session!.PlayerAccountId);
        Assert.True(session.AccessTokenExpiresAtUtc <= Start.AddMinutes(15), "A PC token must not live an hour.");

        var tokens = await fixture.LiveTokensAsync();
        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.Equal(fixture.Device.DeviceId, token.DeviceId));
        Assert.All(tokens, token => Assert.Equal(Start, token.DeviceSignedInAtUtc));

        // Токен настоящий: им пускают в /api/me, как токеном с телефона.
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(me)).StatusCode);
    }

    [Fact]
    public async Task SignIn_WithoutTheDeviceKey_IsUnauthorized()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();

        var response = await fixture.SignInAsync(fixture.Pin, credential: "afk4_not-the-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task WrongPins_FromOnePc_CloseItForAQuarterHour_AcrossDifferentNumbers()
    {
        // Предел ПИН-кода живёт на человеке: пять на номер. Перебор чужих номеров с одной машины
        // он не остановит — это делает счёт на самой машине.
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();

        for (var attempt = 0; attempt < DevicePlayerSignInService.MaxFailedAttempts; attempt++)
        {
            var refused = await fixture.SignInAsync("000000", phone: TestPhones.Next());
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.Equal(
                DevicePlayerSignInErrorCodeNames.SignInRefused,
                (await refused.Content.ReadFromJsonAsync<DevicePlayerSignInErrorDto>())!.Error);
        }

        // Закрыто и для верного ПИН-кода: иначе запрет обходит тот, кто его вызвал.
        var closed = await fixture.SignInAsync(fixture.Pin);
        Assert.Equal(HttpStatusCode.TooManyRequests, closed.StatusCode);
        var error = await closed.Content.ReadFromJsonAsync<DevicePlayerSignInErrorDto>();
        Assert.Equal(DevicePlayerSignInErrorCodeNames.TooManyAttempts, error!.Error);
        Assert.Equal(Start.Add(DevicePlayerSignInService.AttemptWindow), error.RetryAfterUtc);

        fixture.Clock.Advance(DevicePlayerSignInService.AttemptWindow);
        Assert.Equal(HttpStatusCode.OK, (await fixture.SignInAsync(fixture.Pin)).StatusCode);
    }

    [Fact]
    public async Task SignIn_OnAPcWhereAGuestIsPlaying_OpensNothing()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.StartSessionAsync(playerAccountId: null);

        var response = await fixture.SignInAsync(fixture.Pin);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            DevicePlayerSignInErrorCodeNames.SessionNotYours,
            (await response.Content.ReadFromJsonAsync<DevicePlayerSignInErrorDto>())!.Error);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task SignIn_DuringTheirOwnSession_IsLetIn()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.StartSessionAsync(fixture.PlayerAccountId);

        Assert.Equal(HttpStatusCode.OK, (await fixture.SignInAsync(fixture.Pin)).StatusCode);
    }

    [Fact]
    public async Task ANewSignIn_OnTheSamePc_SignsThePreviousPlayerOut()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        var first = await (await fixture.SignInAsync(fixture.Pin)).Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();

        var secondPlayer = await fixture.AddPlayerAsync();
        Assert.Equal(HttpStatusCode.OK, (await fixture.SignInAsync(fixture.Pin, secondPlayer.Phone)).StatusCode);

        var live = await fixture.LiveTokensAsync();
        Assert.DoesNotContain(live, token => token.PlatformPersonId == first!.PlatformPersonId);
        Assert.Contains(live, token => token.PlatformPersonId == secondPlayer.PlatformPersonId);
    }

    [Fact]
    public async Task Refresh_KeepsTheBindingAndTheSignInTime()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        var session = await (await fixture.SignInAsync(fixture.Pin)).Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();

        fixture.Clock.Advance(TimeSpan.FromMinutes(4));
        var refreshed = await fixture.Client.PostAsJsonAsync(
            "/api/public/player/refresh", new PlayerRefreshRequest(session!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var live = await fixture.LiveTokensAsync();
        Assert.All(live, token => Assert.Equal(fixture.Device.DeviceId, token.DeviceId));
        // Время входа не сдвинулось: частое обновление не делает вход вечно свежим.
        Assert.All(live, token => Assert.Equal(Start, token.DeviceSignedInAtUtc));
    }

    [Fact]
    public async Task Heartbeat_OfAFreePc_SignsOutAPlayerWhoNeverStarted()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.SignInAsync(fixture.Pin);

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        await fixture.HeartbeatAsync();
        Assert.NotEmpty(await fixture.LiveTokensAsync());

        fixture.Clock.Advance(EfDeviceBoundPlayerTokens.PreSessionWindow);
        await fixture.HeartbeatAsync();
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task Heartbeat_AfterTheSession_KeepsTheSummaryThenSignsOut()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.SignInAsync(fixture.Pin);
        var sessionId = await fixture.StartSessionAsync(fixture.PlayerAccountId);

        // Играл два часа: вход давний, но пока сессия идёт, его не трогают.
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        await fixture.HeartbeatAsync(sessionId);
        Assert.NotEmpty(await fixture.LiveTokensAsync());

        await fixture.EndSessionAsync(sessionId);
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        await fixture.HeartbeatAsync();
        Assert.NotEmpty(await fixture.LiveTokensAsync());

        fixture.Clock.Advance(EfDeviceBoundPlayerTokens.SummaryWindow);
        await fixture.HeartbeatAsync();
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task Heartbeat_CarriesTheSeatTheOwnerAndTheClubsFeatures()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();

        var idle = await fixture.HeartbeatAsync();
        Assert.Equal(new DeviceSeatDto("ПК 07", "Общий зал"), idle.Seat);
        Assert.Equal(DeviceSessionOwnerKindNames.None, idle.SessionOwner!.Kind);
        Assert.NotNull(idle.Features);

        var sessionId = await fixture.StartSessionAsync(fixture.PlayerAccountId);
        var playing = await fixture.HeartbeatAsync(sessionId);
        Assert.Equal(DeviceSessionOwnerKindNames.Player, playing.SessionOwner!.Kind);
        Assert.Equal(fixture.PlayerAccountId, playing.SessionOwner.PlayerAccountId);
    }

    [Fact]
    public async Task ForcedKeyRotation_SignsThePlayerOutOfThatPc()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.SignInAsync(fixture.Pin);

        var rotated = await fixture.Client.PostAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{fixture.Device.DeviceId:D}/credentials/rotate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task MovingThePcToAnotherSeat_SignsThePlayerOut()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.SignInAsync(fixture.Pin);
        var otherSeat = await fixture.AddSeatAsync("ПК 08");

        var moved = await fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{fixture.Device.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, otherSeat));

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task RemovingThePc_SignsThePlayerOut()
    {
        await using var fixture = Fixture.Create();
        await fixture.SeedAsync();
        await fixture.SignInAsync(fixture.Pin);

        var removed = await fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{fixture.Device.DeviceId:D}/remove",
            new DeviceStateChangeRequest(TestIds.OrganizationId));

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(PlatformApiFactory factory, HttpClient client, MovableTimeProvider clock)
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
        public static Fixture Create()
        {
            var clock = new MovableTimeProvider(Start);
            var factory = new PlatformApiFactory(extraServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
            return new Fixture(factory, factory.CreateClient(), clock);
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

        public async Task<DeviceHeartbeatResponse> HeartbeatAsync(Guid? activeSessionId = null)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{Device.DeviceId}/heartbeat")
            {
                Content = JsonContent.Create(new DeviceHeartbeatRequest(
                    Device.OrganizationId,
                    Device.BranchId,
                    Device.DeviceId,
                    MachineName: "PC-007",
                    AgentVersion: "0.1.0",
                    ShellVersion: "0.1.0",
                    ObservedAtUtc: Clock.GetUtcNow(),
                    IsLocked: activeSessionId is null,
                    ActiveSessionId: activeSessionId,
                    ActiveSessionLeaseExpiresAtUtc: null,
                    ActiveSessionLeaseSequence: null))
            };
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, Device.CredentialSecret);
            var response = await Client.SendAsync(message);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>())!;
        }

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
    }

    private sealed record LiveToken(Guid PlatformPersonId, Guid? DeviceId, DateTimeOffset? DeviceSignedInAtUtc);
}
