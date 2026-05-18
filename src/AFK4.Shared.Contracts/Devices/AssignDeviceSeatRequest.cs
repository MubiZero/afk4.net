namespace AFK4.Shared.Contracts.Devices;

public sealed record AssignDeviceSeatRequest(
    Guid OrganizationId,
    Guid SeatId);
