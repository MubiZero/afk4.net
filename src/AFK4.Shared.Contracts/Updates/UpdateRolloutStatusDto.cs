namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdateRolloutStatusDto(
    Guid UpdateRolloutId,
    Guid OrganizationId,
    Guid BranchId,
    Guid UpdatePackageId,
    string Component,
    string Version,
    string Channel,
    string State,
    string TargetKind,
    IReadOnlyList<Guid> TargetDeviceIds,
    int BatchPercent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<DeviceUpdateStatusSnapshotDto> DeviceStatuses);

public sealed record DeviceUpdateStatusSnapshotDto(
    Guid DeviceId,
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string InstalledVersion,
    string TargetVersion,
    string Status,
    string Message,
    DateTimeOffset UpdatedAtUtc);
