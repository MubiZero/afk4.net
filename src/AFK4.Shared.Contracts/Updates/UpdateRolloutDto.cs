namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdateRolloutDto(
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
    DateTimeOffset? CompletedAtUtc);
