using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionStateMachineTests
{
    [Theory]
    [InlineData(SessionStateNames.Requested, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Requested, SessionStateNames.Failed)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Paused)]
    [InlineData(SessionStateNames.Paused, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Active, SessionStateNames.Ending)]
    [InlineData(SessionStateNames.Paused, SessionStateNames.Ending)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Ended)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Failed)]
    [InlineData(SessionStateNames.Ending, SessionStateNames.Reconciled)]
    [InlineData(SessionStateNames.Failed, SessionStateNames.Reconciled)]
    public void CanTransition_AllowsApprovedTransitions(string from, string to)
    {
        Assert.True(SessionStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(SessionStateNames.Ended, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Reconciled, SessionStateNames.Active)]
    [InlineData(SessionStateNames.Failed, SessionStateNames.Active)]
    public void CanTransition_RejectsTerminalStateRestart(string from, string to)
    {
        Assert.False(SessionStateMachine.CanTransition(from, to));
    }
}
