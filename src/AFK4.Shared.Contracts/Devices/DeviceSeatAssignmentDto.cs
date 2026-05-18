namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceSeatAssignmentDto(
    Guid DeviceSeatAssignmentId,
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid DeviceId,
    DateTimeOffset AttachedAtUtc,
    DateTimeOffset? DetachedAtUtc);
