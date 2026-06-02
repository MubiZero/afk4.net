using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Outbox;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Drains <see cref="OutboxMessageTypes.SessionCheckout"/> rows: the realtime wake-up for the lock
/// command a checkout enqueued. The command row itself is already durable (the agent polls it on
/// heartbeat), so this push is best-effort latency reduction — and idempotent: notifying a device of
/// the same command twice is harmless, so redelivery after a crash is safe.
/// </summary>
public sealed class SessionCheckoutOutboxHandler(IDeviceCommandDispatchService dispatch) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => OutboxMessageTypes.SessionCheckout;

    public async Task<OutboxHandlerResult> HandleAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
    {
        SessionCheckoutNotifyPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionCheckoutNotifyPayload>(message.PayloadJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            return OutboxHandlerResult.PermanentFailure($"Unreadable checkout outbox payload: {exception.Message}");
        }

        if (payload is null || payload.Command is null)
        {
            return OutboxHandlerResult.PermanentFailure("Checkout outbox payload is missing the device command.");
        }

        await dispatch.NotifyAsync(payload.DeviceId, payload.Command, cancellationToken);
        return OutboxHandlerResult.Ok();
    }
}

/// <summary>Payload for a <see cref="OutboxMessageTypes.SessionCheckout"/> outbox row.</summary>
public sealed record SessionCheckoutNotifyPayload(Guid DeviceId, DeviceCommandDto Command);
