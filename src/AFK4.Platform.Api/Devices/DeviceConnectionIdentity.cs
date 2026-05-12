namespace AFK4.Platform.Api.Devices;

public sealed record DeviceConnectionIdentity(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId);
