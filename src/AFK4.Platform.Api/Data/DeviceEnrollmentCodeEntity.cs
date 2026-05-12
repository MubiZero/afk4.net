namespace AFK4.Platform.Api.Data;

public sealed class DeviceEnrollmentCodeEntity
{
    public string Code { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
