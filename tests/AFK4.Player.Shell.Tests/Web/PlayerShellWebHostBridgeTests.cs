using System.Text.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Realtime;
using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerShellWebHostBridgeTests
{
    private sealed class StubAgent : IShellAgentRequests
    {
        public string? LaunchedAppId { get; private set; }

        public List<string> RequestTypes { get; } = [];

        public ShellPipeReplyDto? Reply { get; set; }

        public Task<ShellPipeReplyDto> RequestAsync(
            string type,
            IReadOnlyDictionary<string, string> payload,
            CancellationToken cancellationToken)
        {
            RequestTypes.Add(type);
            if (type == ShellPipeRequestTypeNames.Launch)
            {
                LaunchedAppId = payload["appId"];
            }

            return Task.FromResult(Reply ?? new ShellPipeReplyDto(Guid.NewGuid(), Ok: true));
        }
    }

    private sealed class StubAuth : IPlayerApiAuthClient
    {
        public AuthSnapshot Current { get; private set; }
        public string? CurrentAccessToken => Current.Authenticated ? "tok" : null;
        public Guid? LastOrg { get; private set; }
        public Guid? LastBranch { get; private set; }
        public bool Fail { get; set; }

        public Task<AuthSnapshot> SignInAsync(
            Guid organizationId, string phone, string password, Guid? branchId, CancellationToken ct)
        {
            LastOrg = organizationId;
            LastBranch = branchId;
            Current = Fail ? new AuthSnapshot(false, null, false) : new AuthSnapshot(true, "Alex", true);
            return Task.FromResult(Current);
        }
        public Task EnsureFreshTokenAsync(CancellationToken ct) => Task.CompletedTask;
        public void SignOut() => Current = new AuthSnapshot(false, null, false);
    }

    private static PlayerShellWebHostBridge CreateBridge(StubAgent launcher) =>
        new(launcher, getLatestState: () => null, new StubAuth());

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task LaunchRequest_RoutesAppIdToLauncher()
    {
        var launcher = new StubAgent();
        var bridge = CreateBridge(launcher);

        var request = """{"requestId":"r1","type":"launcher:launch","payload":{"appId":"cs2"}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Equal("cs2", launcher.LaunchedAppId);
        var response = Parse(responseJson!);
        Assert.Equal("r1", response.GetProperty("requestId").GetString());
        Assert.True(response.GetProperty("ok").GetBoolean());
    }

    // Отказ агента доезжает до интерфейса со своим кодом: «игры запускаются только в сессии» и
    // «игры нет на этом ПК» — разные вещи для игрока.
    [Fact]
    public async Task LaunchRequest_RefusedByTheAgent_CarriesTheAgentsReason()
    {
        var agent = new StubAgent
        {
            Reply = new ShellPipeReplyDto(Guid.NewGuid(), Ok: false, ShellPipeErrorCodeNames.NoSession, "Apps start only during a session.")
        };
        var bridge = CreateBridge(agent);

        var responseJson = await bridge.HandleAsync(
            """{"requestId":"r5","type":"launcher:launch","payload":{"appId":"cs2"}}""",
            CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal(ShellPipeErrorCodeNames.NoSession, response.GetProperty("error").GetProperty("code").GetString());
    }

    // Раньше мост отвечал «позвали» и не звал никого.
    [Fact]
    public async Task RequestOperator_AsksTheAgentToCallTheCounter()
    {
        var agent = new StubAgent();
        var bridge = CreateBridge(agent);

        var response = Parse((await bridge.HandleAsync("""{"requestId":"o1","type":"shell:requestOperator"}""", CancellationToken.None))!);

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal([ShellPipeRequestTypeNames.Assist], agent.RequestTypes);
    }

    [Fact]
    public async Task RequestOperator_ThatDidNotReachTheCounter_IsNotReportedAsSent()
    {
        var agent = new StubAgent
        {
            Reply = new ShellPipeReplyDto(Guid.NewGuid(), Ok: false, ShellPipeErrorCodeNames.PlatformUnreachable, "The counter could not be reached.")
        };
        var bridge = CreateBridge(agent);

        var response = Parse((await bridge.HandleAsync("""{"requestId":"o2","type":"shell:requestOperator"}""", CancellationToken.None))!);

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal(ShellPipeErrorCodeNames.PlatformUnreachable, response.GetProperty("error").GetProperty("code").GetString());
    }

    // Паузы у игрока нет: мост отвечал «приостановлено» и не делал ничего.
    [Fact]
    public async Task Pause_IsNoLongerAccepted()
    {
        var bridge = CreateBridge(new StubAgent());

        var response = Parse((await bridge.HandleAsync("""{"requestId":"p1","type":"shell:pause"}""", CancellationToken.None))!);

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_request", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownType_IsRejected()
    {
        var bridge = CreateBridge(new StubAgent());

        var request = """{"requestId":"r2","type":"os:shutdown","payload":{}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_request", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LaunchRequest_MissingAppId_IsRejected()
    {
        var launcher = new StubAgent();
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
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), getLatestState: () => state, new StubAuth());

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

    private static PlayerShellStateDto StateWith(Guid org, Guid? branch = null) =>
        new(
            OrganizationId: org,
            BranchId: branch ?? Guid.NewGuid(),
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
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), () => state, auth);

        var request = """{"requestId":"a1","type":"auth:signIn","payload":{"phoneNumber":"+992900000000","password":"pw"}}""";
        var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(org, auth.LastOrg);
        Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
        Assert.Equal("Alex", response.GetProperty("payload").GetProperty("displayName").GetString());
        Assert.False(response.GetProperty("payload").TryGetProperty("accessToken", out _));
    }

    // Зал у ПК известен самой оболочке. Не назвать его — значит оставить первого гостя сети из
    // нескольких залов без счёта: сервер откажет, а экран покажет то же, что при неверном PIN.
    [Fact]
    public async Task SignIn_NamesTheBranchTheComputerStandsIn()
    {
        var auth = new StubAuth();
        var branch = Guid.NewGuid();
        var state = StateWith(Guid.NewGuid(), branch);
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), () => state, auth);

        var request = """{"requestId":"a3","type":"auth:signIn","payload":{"phoneNumber":"+992900000000","password":"pw"}}""";
        await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Equal(branch, auth.LastBranch);
    }

    // Пустой зал в состоянии — это «не знаю», а не «зал с нулевым идентификатором»: такой
    // отправлять на сервер нельзя, он ответит «нет такого филиала».
    [Fact]
    public async Task SignIn_WithUnknownBranch_SaysNothingAboutIt()
    {
        var auth = new StubAuth();
        var state = StateWith(Guid.NewGuid(), Guid.Empty);
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), () => state, auth);

        var request = """{"requestId":"a4","type":"auth:signIn","payload":{"phoneNumber":"+992900000000","password":"pw"}}""";
        await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Null(auth.LastBranch);
    }

    [Fact]
    public async Task SignIn_WithoutPipeState_IsRejected()
    {
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), getLatestState: () => null, new StubAuth());
        var request = """{"requestId":"a2","type":"auth:signIn","payload":{"phoneNumber":"x","password":"y"}}""";
        var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("no_state", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoadAuthState_ReflectsClient()
    {
        var auth = new StubAuth();
        await auth.SignInAsync(Guid.NewGuid(), "p", "pw", null, CancellationToken.None);
        var bridge = new PlayerShellWebHostBridge(new StubAgent(), () => StateWith(Guid.NewGuid()), auth);

        var response = Parse((await bridge.HandleAsync("""{"requestId":"a3","type":"auth:loadState"}""", CancellationToken.None))!);
        Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
    }
}
