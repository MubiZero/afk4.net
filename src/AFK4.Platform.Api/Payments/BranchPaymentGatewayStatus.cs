namespace AFK4.Platform.Api.Payments;

// Lifecycle of a per-card dcgate gateway.
public static class BranchPaymentGatewayStatus
{
    // Project created in dcgate, but its Telegram session is not yet attached/online.
    public const string PendingTelegram = "pending_telegram";

    // Telegram attached and online; online top-up is allowed.
    public const string Active = "active";

    // Owner-disabled; outbound is refused but late inbound webhooks still verify.
    public const string Disabled = "disabled";
}
