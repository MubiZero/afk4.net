namespace AFK4.Shared.Contracts.Devices;

public sealed record RenameDeviceRequest(
    Guid OrganizationId,
    string DisplayName);
