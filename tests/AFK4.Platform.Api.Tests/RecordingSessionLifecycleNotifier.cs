using System.Collections.Generic;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Tests;

internal sealed class RecordingSessionLifecycleNotifier : ISessionLifecycleNotifier
{
    public List<SessionLifecycleChangedDto> Events { get; } = [];

    public Task NotifyAsync(SessionLifecycleChangedDto change, CancellationToken cancellationToken)
    {
        Events.Add(change);
        return Task.CompletedTask;
    }
}
