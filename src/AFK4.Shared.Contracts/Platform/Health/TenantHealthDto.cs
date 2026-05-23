namespace AFK4.Shared.Contracts.Platform.Health;

public sealed record TenantHealthDto(
    Guid OrganizationId,
    string Status,
    int BranchCount,
    int DeviceCount,
    int ActiveStaffUserCount,
    DateTimeOffset? LatestStaffSignInAtUtc,
    string? LatestMigration,
    int RecentErrorCount,
    IReadOnlyList<TenantHealthErrorDto> RecentErrors);
