namespace AFK4.Shared.Contracts.Devices;

public sealed record RevokeDeviceCredentialResponse(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CredentialId,
    DateTimeOffset RevokedAtUtc);
