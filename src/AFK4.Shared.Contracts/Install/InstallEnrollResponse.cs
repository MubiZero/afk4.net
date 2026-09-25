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
    DateTimeOffset EnrolledAtUtc)
{
    public string LeaseSigningPublicKeyPem { get; init; } = string.Empty;

    public string UpdatePackageSigningPublicKeyPem { get; init; } = string.Empty;

    /// <summary>На какое место встал ПК. Null — без места: при тихой установке место по имени не нашлось или занято.</summary>
    public string? AssignedSeatName { get; init; }
}
