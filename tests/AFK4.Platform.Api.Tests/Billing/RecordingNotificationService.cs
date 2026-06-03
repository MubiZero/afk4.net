using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>Captures the requests passed to <see cref="INotificationService"/> for assertion.</summary>
internal sealed class RecordingNotificationService : INotificationService
{
    public List<NotificationRequest> Requests { get; } = [];

    public Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new NotificationHandle([Guid.NewGuid()], Created: true));
    }

    public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new NotificationDeliveryResult(new NotificationHandle([Guid.NewGuid()], Created: true), Delivered: true, Error: null));
    }
}
