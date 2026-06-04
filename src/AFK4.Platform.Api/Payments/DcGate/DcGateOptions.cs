namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateOptions
{
    public const string SectionName = "DcGate";

    // dcgate base URL, e.g. https://dcgate.mubi.dev
    public string BaseUrl { get; set; } = string.Empty;

    // Per-project API key sent as Authorization: Bearer.
    public string ApiKey { get; set; } = string.Empty;

    // Shared secret dcgate uses to HMAC-sign webhook bodies.
    public string WebhookSecret { get; set; } = string.Empty;
}
