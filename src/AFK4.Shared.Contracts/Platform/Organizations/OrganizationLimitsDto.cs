namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record OrganizationLimitsDto(
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch);
