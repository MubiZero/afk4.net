using System.Text.Json;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DiagnosticsContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BranchDiagnosticsDto_RoundTrips()
    {
        var result = new BranchDiagnosticsDto(
            OrganizationId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            BranchId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            GeneratedAtUtc: DateTimeOffset.Parse("2026-05-16T10:00:00Z"),
            DeviceSummary: new DeviceDiagnosticsSummaryDto(
                TotalDevices: 12,
                OnlineDevices: 10,
                LockedDevices: 8,
                StaleDevices: 2,
                StaleThresholdSeconds: 300,
                NewestHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-16T09:59:30Z")),
            CommandSummary: new CommandDiagnosticsSummaryDto(
                PendingCommands: 3,
                FailedCommands: 1,
                RecentFailures:
                [
                    new FailedCommandDiagnosticsDto(
                        DeviceId: Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"),
                        MachineName: "PC-010",
                        CommandId: Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd"),
                        Type: "unlock",
                        Status: "Failed",
                        Message: "timed out",
                        UpdatedAtUtc: DateTimeOffset.Parse("2026-05-16T09:58:00Z"))
                ]),
            UpdateSummary: new UpdateDiagnosticsSummaryDto(
                ActiveRollouts: 2,
                InstallingDevices: 1,
                FailedDevices: 1,
                RollbackDevices: 1,
                RecentFailures:
                [
                    new FailedUpdateDiagnosticsDto(
                        DeviceId: Guid.Parse("eeeeeeee-eeee-4eee-eeee-eeeeeeeeeeee"),
                        MachineName: "PC-011",
                        UpdateRolloutId: Guid.Parse("ffffffff-ffff-4fff-ffff-ffffffffffff"),
                        Component: "gaming-pc",
                        TargetVersion: "0.1.0",
                        Status: "failed",
                        Message: "hash mismatch",
                        UpdatedAtUtc: DateTimeOffset.Parse("2026-05-16T09:57:00Z"))
                ]),
            StaleDevices:
            [
                new StaleDeviceDiagnosticsDto(
                    DeviceId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    MachineName: "PC-012",
                    AgentVersion: "0.1.0",
                    ShellVersion: "0.1.0",
                    IsOnline: true,
                    IsLocked: true,
                    LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-16T09:40:00Z"),
                    LastHeartbeatAgeSeconds: 1200)
            ]);

        var copy = JsonSerializer.Deserialize<BranchDiagnosticsDto>(
            JsonSerializer.Serialize(result, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(result.BranchId, copy.BranchId);
        Assert.Equal(12, copy.DeviceSummary.TotalDevices);
        Assert.Equal(3, copy.CommandSummary.PendingCommands);
        Assert.Equal("PC-010", copy.CommandSummary.RecentFailures[0].MachineName);
        Assert.Equal("hash mismatch", copy.UpdateSummary.RecentFailures[0].Message);
        Assert.Single(copy.StaleDevices);
    }

    [Fact]
    public void OrganizationPermissionNames_ExposeDiagnosticsPermission()
    {
        Assert.Equal("organization.diagnostics.view", OrganizationPermissionNames.ViewDiagnostics);
    }
}
