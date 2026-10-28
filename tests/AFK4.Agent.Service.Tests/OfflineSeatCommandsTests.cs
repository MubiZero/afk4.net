using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Офлайн у стойки (docs/roadmap/offline-front-desk.md): без интернета Панель передаёт ПК аренду,
/// подписанную ключом ПК администратора. ПК верит ей, только когда сам без связи, только по ключу
/// своего филиала, не дольше четырёх часов и только новее той, что держит.
/// </summary>
public sealed class OfflineSeatCommandsTests : IDisposable
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-27T15:00:00Z");

    private readonly string directory = Directory.CreateTempSubdirectory("afk4-offline-seat-").FullName;
    private readonly ECDsa clubKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly TrustedOfflineKeys trustedKeys = new();
    private readonly InMemorySessionLeaseStore leaseStore = new();
    private readonly OfflineGraceState graceState = new();
    private readonly AgentRuntimeStateStore runtimeState;
    private readonly OfflineSeatCommands commands;

    public OfflineSeatCommandsTests()
    {
        trustedKeys.Replace([new OfflineLeaseKeyDto("desk-1", clubKey.ExportSubjectPublicKeyInfoPem())]);
        var time = new FixedTime(Now);
        runtimeState = new AgentRuntimeStateStore(directory, time);
        var options = Options.Create(new AgentOptions { OrganizationId = OrganizationId, BranchId = BranchId, DeviceId = DeviceId });
        var validator = new SessionLeaseValidator(options, time, trustedKeys);
        var coordinator = new SessionEnforcementCoordinator(validator, leaseStore, runtimeState, new OpenLock(), time);
        commands = new OfflineSeatCommands(coordinator, validator, leaseStore, runtimeState, graceState, time);
        graceState.RecordSuccessfulContact(Now.AddMinutes(-10), 15);
    }

    public void Dispose()
    {
        clubKey.Dispose();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task WithoutInternet_TheClubKeyOpensThePc_ExtendsIt_AndLocksIt()
    {
        var session = Guid.NewGuid();

        var opened = await commands.ApplyAsync(Lease(session, 1, Now.AddHours(1)), CancellationToken.None);
        Assert.True(opened.Accepted, opened.Reason);
        Assert.False(opened.IsLocked);

        var extended = await commands.ApplyAsync(Lease(session, 2, Now.AddHours(2)), CancellationToken.None);
        Assert.True(extended.Accepted, extended.Reason);
        Assert.Equal(Now.AddHours(2), extended.LeaseExpiresAtUtc);

        var locked = await commands.ApplyAsync(Lease(session, 3, Now.AddMinutes(5), OfflineLeaseSigning.EndedState), CancellationToken.None);
        Assert.True(locked.Accepted, locked.Reason);
        Assert.True(locked.IsLocked);
    }

    // Пока у ПК есть связь с платформой, распоряжается она: Панель с ключом клуба ПК не откроет.
    [Fact]
    public async Task WhileThePcIsOnline_AnOfflineLeaseIsRefused()
    {
        graceState.RecordSuccessfulContact(Now.AddSeconds(-30), 15);

        var result = await commands.ApplyAsync(Lease(Guid.NewGuid(), 1, Now.AddHours(1)), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.True(result.IsLocked);
    }

    [Fact]
    public async Task AKeyFromAnotherClub_ALongLease_OrAReplay_AreRefused()
    {
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var session = Guid.NewGuid();

        Assert.False((await commands.ApplyAsync(Lease(session, 1, Now.AddHours(1), key: stranger), CancellationToken.None)).Accepted);
        Assert.False((await commands.ApplyAsync(Lease(session, 1, Now.AddHours(5)), CancellationToken.None)).Accepted);

        Assert.True((await commands.ApplyAsync(Lease(session, 2, Now.AddHours(1)), CancellationToken.None)).Accepted);
        // Повтор старой аренды (её можно было перехватить в сети) не продлевает и не открывает заново.
        Assert.False((await commands.ApplyAsync(Lease(session, 2, Now.AddHours(3)), CancellationToken.None)).Accepted);
        Assert.False((await commands.ApplyAsync(Lease(Guid.NewGuid(), 1, Now.AddHours(1)), CancellationToken.None)).Accepted);
    }

    // «Конец» тоже подписан: чужой в сети клуба без ключа не выгонит гостя, даже зная номер его сессии.
    [Fact]
    public async Task AnEndSignedByAStranger_DoesNotLockTheGuestOut()
    {
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var session = Guid.NewGuid();
        Assert.True((await commands.ApplyAsync(Lease(session, 1, Now.AddHours(1)), CancellationToken.None)).Accepted);

        var forged = await commands.ApplyAsync(
            Lease(session, 2, Now.AddMinutes(5), OfflineLeaseSigning.EndedState, stranger), CancellationToken.None);

        Assert.False(forged.Accepted);
        Assert.False(forged.IsLocked);
        Assert.Equal(session, forged.ActiveSessionId);
    }

    [Fact]
    public async Task OverTheClubNetwork_OnlyOfflineLeasesAreTaken()
    {
        var platformSigned = Lease(Guid.NewGuid(), 1, Now.AddHours(1)) with { SignatureAlgorithm = "ECDSA-P256-SHA256" };

        Assert.False((await commands.ApplyAsync(platformSigned, CancellationToken.None)).Accepted);
    }

    [Fact]
    public void TrustedKeys_SurviveARestart_AndARevokedKeyGoesAtOnce()
    {
        var stored = new TrustedOfflineKeys(directory);
        stored.Replace([new OfflineLeaseKeyDto("desk-1", "pem-1"), new OfflineLeaseKeyDto("desk-2", "pem-2")]);
        Assert.Equal("pem-2", new TrustedOfflineKeys(directory).FindPublicKeyPem("desk-2"));

        stored.Replace([new OfflineLeaseKeyDto("desk-1", "pem-1")]);
        Assert.Null(new TrustedOfflineKeys(directory).FindPublicKeyPem("desk-2"));
    }

    [Fact]
    public async Task ThePanelReachesThePc_OverHttpOnTheClubNetwork()
    {
        var port = FreePort();
        var options = Options.Create(new AgentOptions
        {
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            DeviceId = DeviceId,
            PlatformBaseUrl = new Uri("https://api.afk4.net"),
            OfflineLanPort = port,
            OfflineLanPrefixHost = "http://127.0.0.1"
        });
        using var listener = new OfflineLanListener(commands, options, NullLogger<OfflineLanListener>.Instance);
        await listener.StartAsync(CancellationToken.None);
        Assert.True(await listener.Listening.WaitAsync(TimeSpan.FromSeconds(10)));
        try
        {
            using var http = new HttpClient();
            var url = $"http://127.0.0.1:{port}{OfflineLeaseSigning.AgentLeasePath}";

            var accepted = await http.PostAsJsonAsync(url, Lease(Guid.NewGuid(), 1, Now.AddHours(1)));
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.False((await accepted.Content.ReadFromJsonAsync<OfflineSeatResponse>())!.IsLocked);

            var refused = await http.PostAsJsonAsync(url, Lease(Guid.NewGuid(), 1, Now.AddHours(1)));
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url)).StatusCode);

            // Мусор из сети клуба (найдено на VM 28.10: «{}» ронял обработку в 500) — отказ, а не падение.
            using var empty = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var garbage = await http.PostAsync(url, empty);
            Assert.Equal(HttpStatusCode.Conflict, garbage.StatusCode);
        }
        finally
        {
            await listener.StopAsync(CancellationToken.None);
        }
    }

    private SessionLeaseDto Lease(Guid sessionId, int sequence, DateTimeOffset expiresAt, string state = "Active", ECDsa? key = null)
    {
        var unsigned = new SessionLeaseDto(
            sessionId, OrganizationId, BranchId, SeatId, DeviceId, state, sequence,
            IssuedAtUtc: Now, ExpiresAtUtc: expiresAt,
            SignatureAlgorithm: OfflineLeaseSigning.Algorithm("desk-1"), Signature: string.Empty);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(unsigned);
        var signature = (key ?? clubKey).SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256);
        return unsigned with { Signature = Convert.ToBase64String(signature) };
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private sealed class OpenLock : IWorkstationLockController
    {
        public Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken) => Task.FromResult(WorkstationLockOutcome.Nothing);

        public Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken) => Task.FromResult(WorkstationLockOutcome.Nothing);

        public Task<WorkstationLockOutcome> OpenForSessionAsync(CancellationToken cancellationToken) => Task.FromResult(WorkstationLockOutcome.Nothing);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
