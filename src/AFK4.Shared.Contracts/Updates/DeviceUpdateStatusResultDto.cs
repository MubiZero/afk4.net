namespace AFK4.Shared.Contracts.Updates;

public sealed record DeviceUpdateStatusResultDto(
    Guid DeviceId,
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string Status,
    string Message,
    DateTimeOffset UpdatedAtUtc);
