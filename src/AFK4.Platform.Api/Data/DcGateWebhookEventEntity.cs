namespace AFK4.Platform.Api.Data;

// One row per dcgate webhook delivery we have already processed, keyed by the
// gateway-supplied eventId. Lets the webhook endpoint be idempotent against
// dcgate redeliveries without re-running TopUpWalletAsync.
public sealed class DcGateWebhookEventEntity
{
    public Guid DcGateWebhookEventId { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
