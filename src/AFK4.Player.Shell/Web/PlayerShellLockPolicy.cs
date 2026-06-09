using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

public static class PlayerShellLockPolicy
{
    // Fail-locked / default-deny: no authoritative state ⇒ locked.
    public static bool IsLocked(PlayerShellStateDto? state)
    {
        if (state is null)
        {
            return true;
        }

        return state.State is PlayerShellStateNames.Locked
            or PlayerShellStateNames.Offline
            or PlayerShellStateNames.Error;
    }
}
