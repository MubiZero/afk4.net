using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IAgentUpdateClient
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(
        IReadOnlyList<DeviceComponentVersionDto> installedComponents,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken);

    Task<DeviceUpdateStatusResultDto> ReportStatusAsync(
        Guid updateRolloutId,
        Guid updatePackageId,
        string component,
        string installedVersion,
        string targetVersion,
        string status,
        string message,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}
