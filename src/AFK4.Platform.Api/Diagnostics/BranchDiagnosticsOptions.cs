namespace AFK4.Platform.Api.Diagnostics;

public sealed class BranchDiagnosticsOptions
{
    public int StaleHeartbeatSeconds { get; init; } = 300;

    public int RecentFailureLimit { get; init; } = 10;
}
