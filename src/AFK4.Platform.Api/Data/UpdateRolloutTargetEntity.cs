namespace AFK4.Platform.Api.Data;

public sealed class UpdateRolloutTargetEntity
{
    public Guid UpdateRolloutTargetId { get; set; }

    public Guid UpdateRolloutId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? BranchId { get; set; }

    public string TargetKind { get; set; } = string.Empty;

    public Guid? DeviceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
