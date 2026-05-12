namespace AFK4.Shared.Contracts.Devices;

public sealed record RotateDeviceCredentialResponse(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CredentialId,
    string CredentialSecret,
    DateTimeOffset RotatedAtUtc);
