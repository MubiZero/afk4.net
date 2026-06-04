namespace AFK4.Platform.Api.Data;

// One row per dcgate project (= one card). Bound to a branch; a null BranchId is the
// org-level fallback used by branches that have no card of their own. The dcgate apiKey
// (outbound) and webhook secret (inbound HMAC) are stored encrypted via ISecretProtector.
public sealed class BranchPaymentGatewayEntity
{
    public Guid BranchPaymentGatewayId { get; set; }

    public Guid OrganizationId { get; set; }

    // null => organization-level gateway (fallback for branches without their own card).
    public Guid? BranchId { get; set; }

    // dcgate project id; matches the x-dcgate-project-id webhook header.
    public string DcgateProjectId { get; set; } = string.Empty;

    public string ApiKeyEncrypted { get; set; } = string.Empty;

    public string WebhookSecretEncrypted { get; set; } = string.Empty;

    // Display only; the full card number lives in dcgate, never here.
    public string CardLast4 { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
