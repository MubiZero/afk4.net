namespace AFK4.Shared.Contracts.Devices;

public sealed record InstalledAppReportRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    DateTimeOffset ReportedAtUtc,
    IReadOnlyList<InstalledAppDto> Apps);
