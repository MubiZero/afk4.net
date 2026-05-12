namespace AFK4.Platform.Api.Data;

public sealed class DeviceCredentialEntity
{
    public Guid CredentialId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
