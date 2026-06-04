namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateOptions
{
    public const string SectionName = "DcGate";

    // dcgate base URL, e.g. https://dcgate.mubi.dev
    public string BaseUrl { get; set; } = string.Empty;

    // dcgate ADMIN_JWT_SECRET — sent as the x-admin-secret header on /api/admin/* calls.
    // Empty => owner provisioning/attach is disabled (fail-safe, like the encryption key).
    public string AdminSecret { get; set; } = string.Empty;

    // Full public webhook URL stamped into newly provisioned dcgate projects,
    // e.g. https://afk4.staging.mubi.dev/api/public/payments/dcgate/webhook
    public string WebhookUrl { get; set; } = string.Empty;

    // Payment-link expiry stamped on provisioned projects.
    public int PaymentExpiresInMinutes { get; set; } = 30;
}
