namespace AFK4.Shared.Contracts.Shell;

public static class PlayerShellWarning
{
    // Maps the shell's coarse state + remaining-time into a typed warning kind so the kiosk can
    // pick a localized message.
    //
    // Grace is the agent's offline grace window: the lease lapsed while the platform was out of
    // reach, and the paid session carries on locally (GraceModeMonitor). It used to be classified
    // as CreditLimit on the belief that grace meant the auto-protection lock — so a player whose
    // club Wi-Fi dropped was told their debt had hit the limit. A credit-limit warning arrives from
    // the server as a `warn` command and is not a state of this machine.
    public static string Classify(string state, int? remainingSeconds, int warningThresholdSeconds, bool isGraceMode)
    {
        if (isGraceMode
            || string.Equals(state, PlayerShellStateNames.Grace, StringComparison.Ordinal)
            || string.Equals(state, PlayerShellStateNames.Offline, StringComparison.Ordinal))
        {
            return PlayerShellWarningKinds.Connectivity;
        }

        var sessionRuns = string.Equals(state, PlayerShellStateNames.Active, StringComparison.Ordinal)
            || string.Equals(state, PlayerShellStateNames.Ending, StringComparison.Ordinal);
        if (sessionRuns && remainingSeconds is not null && remainingSeconds <= warningThresholdSeconds)
        {
            return PlayerShellWarningKinds.LowTime;
        }

        return PlayerShellWarningKinds.None;
    }
}
