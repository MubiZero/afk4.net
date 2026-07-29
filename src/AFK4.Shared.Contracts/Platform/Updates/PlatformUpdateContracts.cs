namespace AFK4.Shared.Contracts.Platform.Updates;

public static class PlatformUpdateTargetKindNames
{
    public const string Organization = "organization";

    public const string Branch = "branch";

    public const string Device = "device";
}

public sealed record CreatePlatformUpdatePackageRequest(
    string Component,
    string Version,
    string Channel,
    string ArtifactUri,
    string Sha256,
    string Signature,
    string SignatureAlgorithm,
    long SizeBytes,
    string ReleaseNotes);

public sealed record ChangePlatformUpdatePackageStateRequest(
    string State,
    string Reason);

public sealed record PlatformUpdatePackageDto(
    Guid UpdatePackageId,
    string Component,
    string Version,
    string Channel,
    string ArtifactUri,
    string Sha256,
    string Signature,
    string SignatureAlgorithm,
    long SizeBytes,
    string State,
    string ReleaseNotes,
    Guid CreatedByPlatformAdminUserId,
    DateTimeOffset CreatedAtUtc,
    Guid? ValidatedByPlatformAdminUserId,
    DateTimeOffset? ValidatedAtUtc,
    DateTimeOffset? RetiredAtUtc);

public sealed record CreatePlatformUpdateRolloutRequest(
    Guid UpdatePackageId,
    string Channel,
    string TargetKind,
    IReadOnlyList<Guid> OrganizationIds,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DeviceIds,
    int BatchPercent,
    DateTimeOffset StartsAtUtc,
    string Reason);

public sealed record ChangePlatformUpdateRolloutStateRequest(
    string State,
    string Reason);

public sealed record PlatformUpdateRolloutDto(
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string Version,
    string Channel,
    string State,
    string TargetKind,
    IReadOnlyList<Guid> OrganizationIds,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DeviceIds,
    int BatchPercent,
    string Reason,
    Guid CreatedByPlatformAdminUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? CompletedAtUtc);
