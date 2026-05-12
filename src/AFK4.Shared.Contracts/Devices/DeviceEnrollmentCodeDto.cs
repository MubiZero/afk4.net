namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceEnrollmentCodeDto(
    Guid OrganizationId,
    Guid BranchId,
    string Code,
    DateTimeOffset ExpiresAtUtc);
