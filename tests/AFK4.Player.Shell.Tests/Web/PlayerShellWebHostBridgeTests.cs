using System.Text.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerShellWebHostBridgeTests
{
    private sealed class StubLauncher : ILauncherCommandClient
    {
        public string? LaunchedAppId { get; private set; }

        public Task<PlayerShellCommandResultDto> LaunchAsync(string appId, CancellationToken cancellationToken)
        {
            LaunchedAppId = appId;
            return Task.FromResult(new PlayerShellCommandResultDto(Guid.NewGuid(), "accepted", "launched", DateTimeOffset.UtcNow));
        }
    }

    private sealed class StubAuth : IPlayerApiAuthClient
    {
        public AuthSnapshot Current { get; private set; }
        public string? CurrentAccessToken => Current.Authenticated ? "tok" : null;
        public Guid? LastOrg { get; private set; }
        public bool Fail { get; set; }

        public Task<AuthSnapshot> SignInAsync(Guid organizationId, string phone, string password, CancellationToken ct)
        {
            LastOrg = organizationId;
            Current = Fail ? new AuthSnapshot(false, null, false) : new AuthSnapshot(true, "Alex", true);
            return Task.FromResult(Current);
        }
        public Task EnsureFreshTokenAsync(CancellationToken ct) => Task.CompletedTask;
        public void SignOut() => Current = new AuthSnapshot(false, null, false);
    }

    private static PlayerShellWebHostBridge CreateBridge(StubLauncher launcher) =>
        new(launcher, getLatestState: () => null, new StubAuth());

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task LaunchRequest_RoutesAppIdToLauncher()
    {
        var launcher = new StubLauncher();
        var bridge = CreateBridge(launcher);

        var request = """{"requestId":"r1","type":"launcher:launch","payload":{"appId":"cs2"}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Equal("cs2", launcher.LaunchedAppId);
        var response = Parse(responseJson!);
        Assert.Equal("r1", response.GetProperty("requestId").GetString());
        Assert.True(response.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task UnknownType_IsRejected()
    {
        var bridge = CreateBridge(new StubLauncher());

        var request = """{"requestId":"r2","type":"os:shutdown","payload":{}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_request", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LaunchRequest_MissingAppId_IsRejected()
    {
        var launcher = new StubLauncher();
        var bridge = CreateBridge(launcher);

        var request = """{"requestId":"r3","type":"launcher:launch","payload":{}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Null(launcher.LaunchedAppId);
        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_payload", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoadStateRequest_ReturnsCurrentState()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Active,
            SessionId: Guid.NewGuid(),
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(10),
            RemainingSeconds: 600,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "ok",
            LauncherApps: []);
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), getLatestState: () => state, new StubAuth());

        var request = """{"requestId":"r4","type":"shell:loadState"}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal("active", response.GetProperty("payload").GetProperty("state").GetString());
    }

    [Fact]
    public void StateChangedEnvelope_SerializesAsPushMessage()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: false,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "locked",
            LauncherApps: []);

        var json = PlayerShellWebHostBridge.CreateStatePush(state);

        var envelope = Parse(json);
        Assert.Equal("shell:stateChanged", envelope.GetProperty("type").GetString());
        Assert.Equal("locked", envelope.GetProperty("payload").GetProperty("state").GetString());
    }

    private static PlayerShellStateDto StateWith(Guid org) =>
        new(
            OrganizationId: org,
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Active,
            SessionId: Guid.NewGuid(),
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(10),
            RemainingSeconds: 600,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "ok",
            LauncherApps: []);

    [Fact]
    public async Task SignIn_UsesOrgFromPipeState_ReturnsSnapshotWithoutToken()
    {
        var auth = new StubAuth();
        var org = Guid.NewGuid();
        var state = StateWith(org);
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), () => state, auth);

        var request = """{"requestId":"a1","type":"auth:signIn","payload":{"phoneNumber":"+992900000000","password":"pw"}}""";
        var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(org, auth.LastOrg);
        Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
        Assert.Equal("Alex", response.GetProperty("payload").GetProperty("displayName").GetString());
        Assert.False(response.GetProperty("payload").TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task SignIn_WithoutPipeState_IsRejected()
    {
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), getLatestState: () => null, new StubAuth());
        var request = """{"requestId":"a2","type":"auth:signIn","payload":{"phoneNumber":"x","password":"y"}}""";
        var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("no_state", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoadAuthState_ReflectsClient()
    {
        var auth = new StubAuth();
        await auth.SignInAsync(Guid.NewGuid(), "p", "pw", CancellationToken.None);
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), () => StateWith(Guid.NewGuid()), auth);

        var response = Parse((await bridge.HandleAsync("""{"requestId":"a3","type":"auth:loadState"}""", CancellationToken.None))!);
        Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
    }
}
