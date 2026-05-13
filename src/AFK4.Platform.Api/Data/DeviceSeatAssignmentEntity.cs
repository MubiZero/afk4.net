namespace AFK4.Platform.Api.Data;

public sealed class DeviceSeatAssignmentEntity
{
    public Guid DeviceSeatAssignmentId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SeatId { get; set; }

    public Guid DeviceId { get; set; }

    public DateTimeOffset AttachedAtUtc { get; set; }

    public DateTimeOffset? DetachedAtUtc { get; set; }
}
