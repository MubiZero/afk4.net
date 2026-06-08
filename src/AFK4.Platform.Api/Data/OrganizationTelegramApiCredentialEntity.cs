namespace AFK4.Platform.Api.Data;

// One row per (organization, Telegram phone). Holds the owner's Telegram application
// credentials (api_id / api_hash) encrypted via ISecretProtector, reused across every card
// whose bank notifications arrive in that Telegram account.
public sealed class OrganizationTelegramApiCredentialEntity
{
    public Guid OrganizationTelegramApiCredentialId { get; set; }
    public Guid OrganizationId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string ApiIdEncrypted { get; set; } = string.Empty;
    public string ApiHashEncrypted { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
