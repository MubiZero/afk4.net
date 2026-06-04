namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateOptions
{
    public const string SectionName = "DcGate";

    // dcgate base URL, e.g. https://dcgate.mubi.dev
    public string BaseUrl { get; set; } = string.Empty;

    // Shared secret dcgate uses to HMAC-sign webhook bodies.
    // Retained until the webhook endpoint is migrated to per-branch secrets.
    public string WebhookSecret { get; set; } = string.Empty;
}
