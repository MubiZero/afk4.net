using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Diagnostics;

public sealed class EfBranchDiagnosticsService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    BranchDiagnosticsOptions options) : IBranchDiagnosticsService
{
    private static readonly HashSet<string> FailedCommandStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Failed",
        "Rejected"
    };

    public async Task<BranchDiagnosticsDto> GetAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staleAfter = TimeSpan.FromSeconds(options.StaleHeartbeatSeconds);
        var staleCutoff = now - staleAfter;

        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device =>
                device.OrganizationId == organizationId &&
                device.BranchId == branchId)
            .OrderBy(device => device.MachineName)
            .ToListAsync(cancellationToken);
        var deviceIds = devices
            .Select(device => device.DeviceId)
            .ToHashSet();
        var machineNames = devices.ToDictionary(
            device => device.DeviceId,
            device => device.MachineName);

        var staleDevices = devices
            .Where(device => device.LastHeartbeatAtUtc is null || device.LastHeartbeatAtUtc < staleCutoff)
            .OrderBy(device => device.LastHeartbeatAtUtc is null ? 0 : 1)
            .ThenBy(device => device.LastHeartbeatAtUtc)
            .ThenBy(device => device.MachineName)
            .Select(device => new StaleDeviceDiagnosticsDto(
                device.DeviceId,
                device.MachineName,
                device.AgentVersion,
                device.ShellVersion,
                device.IsOnline,
                device.IsLocked,
                device.LastHeartbeatAtUtc,
                device.LastHeartbeatAtUtc is null
                    ? null
                    : Math.Max(0, (long)Math.Floor((now - device.LastHeartbeatAtUtc.Value).TotalSeconds))))
            .ToList();

        var commands = deviceIds.Count == 0
            ? []
            : await dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => deviceIds.Contains(command.DeviceId))
                .ToListAsync(cancellationToken);
        var failedCommands = commands
            .Where(command => FailedCommandStatuses.Contains(command.Status))
            .ToList();

        var updateStatuses = await dbContext.DeviceUpdateStatuses
            .AsNoTracking()
            .Where(status =>
                status.OrganizationId == organizationId &&
                status.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var activeRollouts = await dbContext.UpdateRollouts
            .AsNoTracking()
            .CountAsync(
                rollout =>
                    rollout.OrganizationId == organizationId &&
                    rollout.BranchId == branchId &&
                    rollout.State == UpdateRolloutStateNames.Active,
                cancellationToken);

        var recentFailedCommands = failedCommands
            .OrderByDescending(command => command.UpdatedAtUtc)
            .ThenBy(command => command.CommandId)
            .Take(options.RecentFailureLimit)
            .Select(command => new FailedCommandDiagnosticsDto(
                command.DeviceId,
                machineNames.GetValueOrDefault(command.DeviceId) ?? string.Empty,
                command.CommandId,
                command.Type,
                command.Status,
                command.Message,
                command.UpdatedAtUtc))
            .ToList();

        var recentFailedUpdates = updateStatuses
            .Where(status => IsStatus(status.Status, UpdateStatusNames.Failed))
            .OrderByDescending(status => status.UpdatedAtUtc)
            .ThenBy(status => status.DeviceUpdateStatusId)
            .Take(options.RecentFailureLimit)
            .Select(status => new FailedUpdateDiagnosticsDto(
                status.DeviceId,
                machineNames.GetValueOrDefault(status.DeviceId) ?? string.Empty,
                status.UpdateRolloutId,
                status.Component,
                status.TargetVersion,
                status.Status,
                status.Message,
                status.UpdatedAtUtc))
            .ToList();

        return new BranchDiagnosticsDto(
            organizationId,
            branchId,
            now,
            new DeviceDiagnosticsSummaryDto(
                TotalDevices: devices.Count,
                OnlineDevices: devices.Count(device => device.IsOnline),
                LockedDevices: devices.Count(device => device.IsLocked),
                StaleDevices: staleDevices.Count,
                StaleThresholdSeconds: options.StaleHeartbeatSeconds,
                NewestHeartbeatAtUtc: devices
                    .Where(device => device.LastHeartbeatAtUtc is not null)
                    .Select(device => device.LastHeartbeatAtUtc)
                    .Max()),
            new CommandDiagnosticsSummaryDto(
                PendingCommands: commands.Count(command => string.Equals(command.Status, "Pending", StringComparison.OrdinalIgnoreCase)),
                FailedCommands: failedCommands.Count,
                RecentFailures: recentFailedCommands),
            new UpdateDiagnosticsSummaryDto(
                ActiveRollouts: activeRollouts,
                InstallingDevices: updateStatuses.Count(status => IsStatus(status.Status, UpdateStatusNames.Installing)),
                FailedDevices: updateStatuses.Count(status => IsStatus(status.Status, UpdateStatusNames.Failed)),
                RollbackDevices: updateStatuses.Count(status =>
                    IsStatus(status.Status, UpdateStatusNames.RollbackStarted) ||
                    IsStatus(status.Status, UpdateStatusNames.RolledBack)),
                RecentFailures: recentFailedUpdates),
            staleDevices);
    }

    private static bool IsStatus(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
