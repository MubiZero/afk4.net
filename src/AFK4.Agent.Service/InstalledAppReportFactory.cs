using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class InstalledAppReportFactory
{
    public static InstalledAppReportRequest Create(
        AgentOptions options,
        IReadOnlyCollection<InstalledAppSnapshot> apps,
        DateTimeOffset reportedAtUtc)
    {
        return new InstalledAppReportRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            ReportedAtUtc: reportedAtUtc,
            Apps: apps
                .Where(app => !string.IsNullOrWhiteSpace(app.DisplayName))
                .Select(app => new InstalledAppDto(
                    DisplayName: app.DisplayName.Trim(),
                    Version: string.IsNullOrWhiteSpace(app.Version) ? null : app.Version.Trim(),
                    Publisher: string.IsNullOrWhiteSpace(app.Publisher) ? null : app.Publisher.Trim(),
                    InstallLocation: string.IsNullOrWhiteSpace(app.InstallLocation) ? null : app.InstallLocation.Trim(),
                    InstalledAtUtc: app.InstalledAtUtc))
                .ToArray());
    }
}
