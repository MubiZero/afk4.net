namespace AFK4.Shared.Contracts.Devices;

public sealed record MoveDeviceSeatRequest(
    Guid OrganizationId,
    Guid SeatId);
