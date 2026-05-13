using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public static class SessionStateMachine
{
    private static readonly IReadOnlySet<(string From, string To)> AllowedTransitions =
        new HashSet<(string From, string To)>
        {
            (SessionStateNames.Requested, SessionStateNames.Active),
            (SessionStateNames.Requested, SessionStateNames.Failed),
            (SessionStateNames.Active, SessionStateNames.Active),
            (SessionStateNames.Active, SessionStateNames.Paused),
            (SessionStateNames.Paused, SessionStateNames.Active),
            (SessionStateNames.Active, SessionStateNames.Ending),
            (SessionStateNames.Paused, SessionStateNames.Ending),
            (SessionStateNames.Ending, SessionStateNames.Ended),
            (SessionStateNames.Ending, SessionStateNames.Failed),
            (SessionStateNames.Ending, SessionStateNames.Reconciled),
            (SessionStateNames.Failed, SessionStateNames.Reconciled)
        };

    public static bool CanTransition(string from, string to)
    {
        return AllowedTransitions.Contains((from, to));
    }
}
