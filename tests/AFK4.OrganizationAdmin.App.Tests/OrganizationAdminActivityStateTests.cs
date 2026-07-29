using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminActivityStateTests
{
    [Fact]
    public void CriticalCommandLeases_AreReferenceCountedAndIdempotent()
    {
        var state = new OrganizationAdminActivityState();
        var first = state.BeginCriticalCommand();
        var second = state.BeginCriticalCommand();
        Assert.Equal(LocalUpdateCoordinationStatuses.CriticalCommandActive, state.QueryStatus());

        first.Dispose(); first.Dispose();
        Assert.Equal(LocalUpdateCoordinationStatuses.CriticalCommandActive, state.QueryStatus());
        second.Dispose();

        Assert.Equal(LocalUpdateCoordinationStatuses.Idle, state.QueryStatus());
    }
}
