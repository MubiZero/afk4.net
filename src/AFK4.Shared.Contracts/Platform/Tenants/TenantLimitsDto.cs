namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record TenantLimitsDto(
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch);
