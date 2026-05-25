namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceStateChangeRequest(
    Guid OrganizationId,
    string? Reason = null);
