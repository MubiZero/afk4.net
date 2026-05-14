namespace AFK4.Shared.Contracts.Updates;

public sealed record CreateUpdateRolloutRequest(
    Guid OrganizationId,
    Guid UpdatePackageId,
    string Channel,
    string TargetKind,
    IReadOnlyList<Guid> TargetDeviceIds,
    int BatchPercent,
    DateTimeOffset StartsAtUtc,
    string Reason);
