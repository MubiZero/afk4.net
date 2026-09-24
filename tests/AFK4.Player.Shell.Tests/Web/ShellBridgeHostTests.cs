using System.Net.Http;
using System.Text.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Player.Shell.Realtime;
using AFK4.Player.Shell.Web;
using AFK4.Player.Shell.Workstation;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>
/// Мост v2 к странице (спека оболочки, §4.4): имена и тела — из контрактов, коды отказов агента
/// доходят до экрана как есть, а вход сообщается событием, а не ответом.
/// </summary>
public sealed class ShellBridgeHostTests
{
    [Fact]
    public async Task ShellReady_AnswersWithWhatTheHostAlreadyKnows()
    {
        var fixture = new Fixture();
        fixture.State = State();
        fixture.Session.Accept(Session());

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.ShellReady);

        Assert.True(response.GetProperty("ok").GetBoolean());
        var payload = response.GetProperty("payload");
        Assert.Equal(PlayerShellStateNames.Locked, payload.GetProperty("state").GetProperty("state").GetString());
        Assert.True(payload.GetProperty("auth").GetProperty("signedIn").GetBoolean());
        Assert.Equal("Фарход", payload.GetProperty("auth").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task SignIn_GoesToTheAgent_AndTheTokensNeverReachThePage()
    {
        var fixture = new Fixture();

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.AuthSignIn, new { phone = "+992900000007", pin = "123456" });

        Assert.True(response.GetProperty("ok").GetBoolean());
        var (type, payload) = Assert.Single(fixture.Agent.Requests);
        Assert.Equal(ShellPipeRequestTypeNames.SignInPin, type);
        Assert.Equal("+992900000007", payload["phone"]);
        Assert.Equal("123456", payload["pin"]);
    }

    [Theory]
    [InlineData(ShellPipeErrorCodeNames.SignInRefused)]
    [InlineData(ShellPipeErrorCodeNames.TooManyAttempts)]
    [InlineData(ShellPipeErrorCodeNames.PlatformUnreachable)]
    [InlineData(ShellPipeErrorCodeNames.AgentUnavailable)]
    public async Task TheAgentsRefusal_ReachesThePageUnderTheSameCode(string code)
    {
        var fixture = new Fixture();
        fixture.Agent.Reply = new ShellPipeReplyDto(Guid.NewGuid(), Ok: false, code, "no");

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.AuthSignIn, new { phone = "+992900000007", pin = "000000" });

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal(code, response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SignIn_WithoutAPin_DoesNotBotherTheAgent()
    {
        var fixture = new Fixture();

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.AuthSignIn, new { phone = "+992900000007" });

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Empty(fixture.Agent.Requests);
    }

    [Fact]
    public async Task SignOut_ForgetsThePlayer_AndTellsThePage()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session());
        ShellAuthStateDto? announced = null;
        fixture.Bridge.AuthChanged += auth => announced = auth;

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.AuthSignOut);

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.False(fixture.Session.Current.SignedIn);
        Assert.False(announced!.SignedIn);
    }

    [Fact]
    public async Task Launch_AndAssist_AreAskedOfTheAgent()
    {
        var fixture = new Fixture();

        await fixture.SendAsync(ShellBridgeRequestTypeNames.AppLaunch, new { appId = "cs2" });
        await fixture.SendAsync(ShellBridgeRequestTypeNames.AssistCall);

        Assert.Equal(
            [ShellPipeRequestTypeNames.Launch, ShellPipeRequestTypeNames.Assist],
            fixture.Agent.Requests.Select(request => request.Type));
        Assert.Equal("cs2", fixture.Agent.Requests[0].Payload["appId"]);
    }

    [Fact]
    public async Task ARequestTheHostCannotServeYet_IsRefusedHonestly()
    {
        var response = await new Fixture().SendAsync(ShellBridgeRequestTypeNames.ShowcaseImpression, new { slideId = "promo" });

        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal(ShellBridgeErrorCodeNames.NotSupported, response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ShellReady_IncludesSoundAndLayout()
    {
        var fixture = new Fixture(new FakeSystemControls());

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.ShellReady);

        var system = response.GetProperty("payload").GetProperty("system");
        Assert.Equal(60, system.GetProperty("volume").GetInt32());
        Assert.Equal("RU", system.GetProperty("layout").GetString());
    }

    [Fact]
    public async Task Volume_IsSet_AndTheAnswerIsWhatThePcNowHas()
    {
        var system = new FakeSystemControls();
        var fixture = new Fixture(system);

        var response = await fixture.SendAsync(ShellBridgeRequestTypeNames.SystemSetVolume, new { volume = 25 });

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(25, system.State.Volume);
        Assert.Equal(25, response.GetProperty("payload").GetProperty("volume").GetInt32());
    }

    [Fact]
    public async Task Mic_IsMuted()
    {
        var system = new FakeSystemControls();

        await new Fixture(system).SendAsync(ShellBridgeRequestTypeNames.SystemSetMicMuted, new { micMuted = true });

        Assert.True(system.State.MicMuted);
    }

    [Fact]
    public async Task ALayoutChange_IsAnsweredWithTheRequestedLayout_BeforeWindowsCatchesUp()
    {
        // Windows меняет раскладку сообщением окну: чтение сразу вернуло бы старую, и кнопка мигнула бы назад.
        var system = new FakeSystemControls { LayoutLags = true };

        var response = await new Fixture(system).SendAsync(ShellBridgeRequestTypeNames.SystemSetLayout, new { layout = "TG" });

        Assert.Equal("TG", response.GetProperty("payload").GetProperty("layout").GetString());
        Assert.Equal(["TG"], system.LayoutRequests);
    }

    [Theory]
    [InlineData(ShellBridgeRequestTypeNames.SystemSetLayout, "{\"layout\":\"DE\"}")]
    [InlineData(ShellBridgeRequestTypeNames.SystemSetVolume, "{\"volume\":\"loud\"}")]
    [InlineData(ShellBridgeRequestTypeNames.SystemSetMicMuted, "{}")]
    public async Task ANonsenseChange_IsRefused_AndNothingIsTouched(string type, string payloadJson)
    {
        var system = new FakeSystemControls();
        var fixture = new Fixture(system);

        var json = await fixture.Bridge.HandleAsync(
            $"{{\"type\":\"{type}\",\"requestId\":\"r-1\",\"payload\":{payloadJson}}}", CancellationToken.None);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(new ShellSystemStateDto(60, false, "RU"), system.State);
    }

    [Fact]
    public async Task WindowsRefusing_IsSaidPlainly()
    {
        var system = new FakeSystemControls { Refuse = true };

        var response = await new Fixture(system).SendAsync(ShellBridgeRequestTypeNames.SystemSetMicMuted, new { micMuted = true });

        Assert.Equal(ShellBridgeErrorCodeNames.SystemUnavailable, response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheChosenLanguage_IsKept_UntilThePlayerLeaves()
    {
        var fixture = new Fixture();

        await fixture.SendAsync(ShellBridgeRequestTypeNames.UiSetLocale, new { locale = "tg" });
        Assert.Equal("tg", fixture.Bridge.Locale);

        await fixture.SendAsync(ShellBridgeRequestTypeNames.AuthSignOut);
        Assert.Null(fixture.Bridge.Locale);
    }

    [Fact]
    public void AnEvent_IsTypeAndPayload()
    {
        using var document = JsonDocument.Parse(ShellBridgeHost.Event(ShellBridgeEventTypeNames.AuthChanged, new ShellAuthStateDto(false)));

        Assert.Equal(ShellBridgeEventTypeNames.AuthChanged, document.RootElement.GetProperty("type").GetString());
        Assert.False(document.RootElement.GetProperty("payload").GetProperty("signedIn").GetBoolean());
    }

    private static PlayerShellStateDto State() => new(
        OrganizationId: Guid.NewGuid(),
        BranchId: Guid.NewGuid(),
        DeviceId: Guid.NewGuid(),
        State: PlayerShellStateNames.Locked,
        SessionId: null,
        LeaseExpiresAtUtc: null,
        RemainingSeconds: null,
        IsOnline: true,
        IsGraceMode: false,
        WarningThresholdSeconds: 300,
        Message: "locked",
        LauncherApps: []);

    private static PlatformPersonSessionResponse Session() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Фарход", true, "access-token", DateTimeOffset.UtcNow.AddMinutes(15),
        "refresh-token", DateTimeOffset.UtcNow.AddHours(12), Guid.NewGuid(), "ru", true);

    private sealed class Fixture
    {
        public Fixture(ISystemControls? system = null)
        {
            // Без адреса API выход не ходит на сервер — мосту это и не нужно проверять.
            Session = new DevicePlayerSession(new HttpClient(), () => null, TimeProvider.System);
            Bridge = new ShellBridgeHost(Agent, Session, () => State, system);
        }

        public RecordingAgent Agent { get; } = new();

        public DevicePlayerSession Session { get; }

        public ShellBridgeHost Bridge { get; }

        public PlayerShellStateDto? State { get; set; }

        public async Task<JsonElement> SendAsync(string type, object? payload = null)
        {
            var json = await Bridge.HandleAsync(
                JsonSerializer.Serialize(new { type, requestId = "r-1", payload = payload ?? new { } }),
                CancellationToken.None);
            using var document = JsonDocument.Parse(json);
            Assert.Equal("host:response", document.RootElement.GetProperty("type").GetString());
            Assert.Equal("r-1", document.RootElement.GetProperty("requestId").GetString());
            return document.RootElement.Clone();
        }
    }

    private sealed class FakeSystemControls : ISystemControls
    {
        public ShellSystemStateDto State { get; private set; } = new(60, false, "RU");

        public bool Refuse { get; init; }

        public bool LayoutLags { get; init; }

        public List<string> LayoutRequests { get; } = [];

        public ShellSystemStateDto Read() => State;

        public void SetVolume(int percent) => State = Refuse ? throw new InvalidOperationException() : State with { Volume = percent };

        public void SetMicMuted(bool muted) => State = Refuse ? throw new InvalidOperationException() : State with { MicMuted = muted };

        public void SetLayout(string label)
        {
            LayoutRequests.Add(label);
            if (!LayoutLags)
            {
                State = State with { Layout = label };
            }
        }
    }

    private sealed class RecordingAgent : IShellAgentRequests
    {
        public ShellPipeReplyDto Reply { get; set; } = new(Guid.NewGuid(), Ok: true);

        public List<(string Type, IReadOnlyDictionary<string, string> Payload)> Requests { get; } = [];

        public Task<ShellPipeReplyDto> RequestAsync(string type, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
        {
            Requests.Add((type, payload));
            return Task.FromResult(Reply);
        }
    }
}
