namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceEnrollmentResponse(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CredentialId,
    string CredentialSecret,
    DateTimeOffset EnrolledAtUtc);
