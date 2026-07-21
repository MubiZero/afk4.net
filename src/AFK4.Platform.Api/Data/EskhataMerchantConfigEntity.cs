namespace AFK4.Platform.Api.Data;

// Eskhata Merchant credentials for a tenant. Separate from BranchPaymentGatewayEntity (which is
// dcgate card-shaped): Eskhata is merchant-config-shaped. A null BranchId is the org-level config.
// The Hash key is stored encrypted via ISecretProtector, like the dcgate apiKey. This slice only
// stores the credentials (UI prep); the Merchant API flow (invoice/status/webhook/credit) is a
// separate deferred epic, so Status is never "active" here.
public sealed class EskhataMerchantConfigEntity
{
    public Guid EskhataMerchantConfigId { get; set; }

    public Guid OrganizationId { get; set; }

    // null => organization-level config (fallback for branches without their own).
    public Guid? BranchId { get; set; }

    // Merchant API root URL without a trailing slash.
    public string BaseUrl { get; set; } = string.Empty;

    // Company ID in its raw form; base64 encoding for the X-CompanyId header is the client's job.
    public string CompanyId { get; set; } = string.Empty;

    // Идентификатор торговой точки (merchantId) для orderTypeId=3 (DynamicPos).
    // posId при типе 3 не задаётся оператором — банк возвращает его в ответе на create.
    public int MerchantId { get; set; }

    public string HashKeyEncrypted { get; set; } = string.Empty;

    // configured | inactive — never "active" in the UI-prep slice.
    public string Status { get; set; } = EskhataMerchantConfigStatus.Inactive;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public static class EskhataMerchantConfigStatus
{
    // All four credentials present — ready for the future activation epic, but not yet live.
    public const string Configured = "configured";

    // Missing credentials or never fully saved.
    public const string Inactive = "inactive";
}
