using System.Text.Json;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PlayerShellContractSerializationTests
{
    [Fact]
    public void LockedState_RoundTripsWithoutSession()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        var json = JsonSerializer.Serialize(state);
        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellStateNames.Locked, copy.State);
        Assert.Null(copy.SessionId);
        Assert.Empty(copy.LauncherApps);
    }

    [Fact]
    public void ActiveState_RoundTripsSessionCountdownAndLauncherApps()
    {
        var app = new LauncherAppDto(
            AppId: "counter-strike-2",
            DisplayName: "Counter-Strike 2",
            Category: "Games",
            IconUri: "afk4://icons/cs2",
            IsAvailable: true);
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: PlayerShellStateNames.Active,
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            LeaseExpiresAtUtc: DateTimeOffset.Parse("2026-05-14T10:15:00Z"),
            RemainingSeconds: 1800,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session is active.",
            LauncherApps: [app]);

        var json = JsonSerializer.Serialize(state);
        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellStateNames.Active, copy.State);
        Assert.Equal(state.SessionId, copy.SessionId);
        Assert.Equal(1800, copy.RemainingSeconds);
        Assert.Single(copy.LauncherApps);
        Assert.Equal("counter-strike-2", copy.LauncherApps[0].AppId);
    }

    [Fact]
    public void LauncherCommand_RoundTripsRequestAndResultCorrelation()
    {
        var command = new PlayerShellCommandDto(
            CommandId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            Type: "launch-app",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["appId"] = "counter-strike-2"
            });
        var result = new PlayerShellCommandResultDto(
            CommandId: command.CommandId,
            Status: "Accepted",
            Message: "Launch request accepted.",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:01Z"));

        var commandCopy = JsonSerializer.Deserialize<PlayerShellCommandDto>(JsonSerializer.Serialize(command));
        var resultCopy = JsonSerializer.Deserialize<PlayerShellCommandResultDto>(JsonSerializer.Serialize(result));

        Assert.NotNull(commandCopy);
        Assert.NotNull(resultCopy);
        Assert.Equal(command.CommandId, commandCopy.CommandId);
        Assert.Equal("launch-app", commandCopy.Type);
        Assert.Equal("counter-strike-2", commandCopy.Payload["appId"]);
        Assert.Equal(command.CommandId, resultCopy.CommandId);
        Assert.Equal("Accepted", resultCopy.Status);
    }
}
