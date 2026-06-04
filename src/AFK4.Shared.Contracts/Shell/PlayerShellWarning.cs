namespace AFK4.Shared.Contracts.Shell;

public static class PlayerShellWarning
{
    // Maps the shell's coarse state + remaining-time into a typed warning kind so the kiosk can
    // pick a localized message and decide whether to surface the actionable "top up to keep playing"
    // panel. Grace = the auto-protection lock window (credit/limit reached); Offline = connectivity.
    public static string Classify(string state, int? remainingSeconds, int warningThresholdSeconds, bool isGraceMode)
    {
        if (isGraceMode || string.Equals(state, PlayerShellStateNames.Grace, StringComparison.Ordinal))
        {
            return PlayerShellWarningKinds.CreditLimit;
        }

        if (string.Equals(state, PlayerShellStateNames.Offline, StringComparison.Ordinal))
        {
            return PlayerShellWarningKinds.Connectivity;
        }

        if (string.Equals(state, PlayerShellStateNames.Active, StringComparison.Ordinal) &&
            remainingSeconds is not null && remainingSeconds <= warningThresholdSeconds)
        {
            return PlayerShellWarningKinds.LowTime;
        }

        return PlayerShellWarningKinds.None;
    }
}
