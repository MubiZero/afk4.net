using AFK4.Localization;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Shell;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests;

public sealed class PlayerShellViewModelTests
{
    private static PlayerShellViewModel BuildViewModel(ILauncherCommandClient? client = null) =>
        new(client ?? new RecordingLauncherCommandClient(), LocalizationService.LoadEmbedded(Locales.Ru));

    [Fact]
    public void ApplyState_LockedStateShowsLockedScreenAndHidesLauncher()
    {
        var viewModel = BuildViewModel();

        viewModel.ApplyState(CreateState(PlayerShellStateNames.Locked, remainingSeconds: null, launcherApps: []));

        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.ShowLauncher);
        Assert.Equal("This PC is locked.", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyState_ActiveStateShowsRemainingTimeAndLauncher()
    {
        var viewModel = BuildViewModel();
        var app = new LauncherAppDto("counter-strike-2", "Counter-Strike 2", "Games", null, IsAvailable: true);

        viewModel.ApplyState(CreateState(PlayerShellStateNames.Active, remainingSeconds: 1800, launcherApps: [app]));

        Assert.True(viewModel.IsSessionActive);
        Assert.Equal("30:00", viewModel.RemainingTimeText);
        Assert.True(viewModel.ShowLauncher);
        Assert.Single(viewModel.LauncherApps);
    }

    [Fact]
    public void ApplyState_GraceStateShowsOfflineWarning()
    {
        var viewModel = BuildViewModel();

        viewModel.ApplyState(CreateState(PlayerShellStateNames.Grace, remainingSeconds: 120, launcherApps: []));

        Assert.True(viewModel.IsGraceMode);
        Assert.True(viewModel.ShowWarning);
        Assert.Contains("Connection lost", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyState_RendersStatusInBranchLocaleFromState()
    {
        var viewModel = BuildViewModel();

        // The customer-facing status follows the branch locale carried on the state,
        // resolved from the catalog by state name — NOT the (English) dto.Message.
        viewModel.ApplyState(CreateState(
            PlayerShellStateNames.Locked, remainingSeconds: null, launcherApps: [], locale: Locales.Ru));

        Assert.Equal("Этот компьютер заблокирован.", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyState_ShowsWarningBelowThreshold()
    {
        var viewModel = BuildViewModel();

        viewModel.ApplyState(CreateState(PlayerShellStateNames.Active, remainingSeconds: 240, launcherApps: []));

        Assert.True(viewModel.ShowWarning);
    }

    [Fact]
    public async Task LaunchAsync_SendsLauncherCommandThroughInjectedClient()
    {
        var client = new RecordingLauncherCommandClient();
        var viewModel = BuildViewModel(client);
        viewModel.ApplyState(CreateState(
            PlayerShellStateNames.Active,
            remainingSeconds: 1800,
            launcherApps: [new LauncherAppDto("counter-strike-2", "Counter-Strike 2", "Games", null, true)]));

        await viewModel.LaunchAsync(viewModel.LauncherApps[0], CancellationToken.None);

        Assert.Equal("counter-strike-2", client.LastAppId);
        Assert.Equal("Accepted", viewModel.LastCommandStatus);
    }

    private static PlayerShellStateDto CreateState(
        string state,
        int? remainingSeconds,
        IReadOnlyList<LauncherAppDto> launcherApps,
        string locale = "en")
    {
        return new PlayerShellStateDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: state,
            SessionId: state == PlayerShellStateNames.Locked ? null : Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            LeaseExpiresAtUtc: state == PlayerShellStateNames.Locked ? null : DateTimeOffset.Parse("2026-05-14T10:15:00Z"),
            RemainingSeconds: remainingSeconds,
            IsOnline: state != PlayerShellStateNames.Grace,
            IsGraceMode: state == PlayerShellStateNames.Grace,
            WarningThresholdSeconds: 300,
            Message: state == PlayerShellStateNames.Grace ? "Connection lost. Active session continues." : "This PC is locked.",
            LauncherApps: launcherApps,
            Locale: locale);
    }

    private sealed class RecordingLauncherCommandClient : ILauncherCommandClient
    {
        public string? LastAppId { get; private set; }

        public Task<PlayerShellCommandResultDto> LaunchAsync(string appId, CancellationToken cancellationToken)
        {
            LastAppId = appId;
            return Task.FromResult(new PlayerShellCommandResultDto(
                CommandId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
                Status: "Accepted",
                Message: "Launch request accepted.",
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z")));
        }
    }
}
