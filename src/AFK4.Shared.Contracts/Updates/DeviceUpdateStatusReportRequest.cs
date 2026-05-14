namespace AFK4.Shared.Contracts.Updates;

public sealed record DeviceUpdateStatusReportRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string InstalledVersion,
    string TargetVersion,
    string Status,
    string Message,
    DateTimeOffset ObservedAtUtc);
