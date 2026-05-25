namespace AFK4.Shared.Contracts.Install;

public sealed record InstallEnrollResponse(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CredentialId,
    string CredentialSecret,
    string EnrollmentState,
    string ApiBaseUrl,
    string UpdateChannel,
    DateTimeOffset EnrolledAtUtc);
