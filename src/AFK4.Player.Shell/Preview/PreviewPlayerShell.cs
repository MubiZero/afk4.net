#if DEBUG
using System.Runtime.CompilerServices;
using AFK4.Localization;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Preview;

/// <summary>
/// Wires the player-shell window with in-memory fakes so it can be opened and interacted with
/// without connecting to the Agent named pipe or dispatching real launcher commands.
/// Launch with: AFK4.Player.Shell.exe --preview
/// </summary>
internal static class PreviewPlayerShell
{
    private static readonly Guid PreviewOrg = new("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid PreviewBranch = new("00000000-0000-0000-0000-0000000000bb");
    private static readonly Guid PreviewDevice = new("00000000-0000-0000-0000-0000000000cc");
    private static readonly Guid PreviewSession = new("00000000-0000-0000-0000-0000000000dd");

    internal sealed record PreviewContext(
        IPlayerShellStateClient StateClient,
        ILauncherCommandClient Launcher,
        PlayerShellStateDto InitialState);

    public static PreviewContext Create(ILocalizationService localization)
    {
        var initialState = new PlayerShellStateDto(
            OrganizationId: PreviewOrg,
            BranchId: PreviewBranch,
            DeviceId: PreviewDevice,
            State: PlayerShellStateNames.Active,
            SessionId: PreviewSession,
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(47),
            RemainingSeconds: 2820,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session active.",
            LauncherApps:
            [
                new LauncherAppDto("steam", "Steam", "launcher", null, IsAvailable: true),
                new LauncherAppDto("cs2", "Counter-Strike 2", "game", null, IsAvailable: true),
                new LauncherAppDto("dota2", "Dota 2", "game", null, IsAvailable: true),
                new LauncherAppDto("valorant", "Valorant", "game", null, IsAvailable: false),
            ],
            Locale: localization.CurrentLocale);

        return new PreviewContext(new NoOpStateClient(), NoOpLauncherCommandClient.Instance, initialState);
    }

    // Returns an empty async sequence — preview does not connect to the Agent pipe.
    private sealed class NoOpStateClient : IPlayerShellStateClient
    {
        public async IAsyncEnumerable<PlayerShellStateDto> ReadStatesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
#endif
