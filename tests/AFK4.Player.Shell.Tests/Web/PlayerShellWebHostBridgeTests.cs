using System.Text.Json;
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

    private static PlayerShellWebHostBridge CreateBridge(StubLauncher launcher) =>
        new(launcher, getLatestState: () => null);

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
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), getLatestState: () => state);

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
}
