using AFK4.OrganizationAdmin.App.Diagnostics;
using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class DiagnosticsWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public async Task RefreshAsync_WithBranchContext_LoadsSummaryAndRows()
    {
        var apiClient = new RecordingOperatorDiagnosticsApiClient();
        var viewModel = new DiagnosticsWorkspaceViewModel(apiClient);
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastBranchId);
        Assert.Equal(12, viewModel.TotalDevices);
        Assert.Equal(10, viewModel.OnlineDevices);
        Assert.Equal(2, viewModel.StaleDeviceCount);
        Assert.Equal(1, viewModel.FailedCommandCount);
        Assert.Equal(1, viewModel.FailedUpdateCount);
        Assert.Equal("12 devices, 2 stale, 1 failed commands, 1 failed updates.", viewModel.Summary);
        Assert.Equal("PC-STALE", Assert.Single(viewModel.StaleDevices).MachineName);
        Assert.Equal("unlock", Assert.Single(viewModel.FailedCommands).Type);
        Assert.Equal("gaming-pc", Assert.Single(viewModel.FailedUpdates).Component);
        Assert.Equal("Diagnostics loaded.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidBranchId_DoesNotCallBackendAndShowsError()
    {
        var apiClient = new RecordingOperatorDiagnosticsApiClient();
        var viewModel = new DiagnosticsWorkspaceViewModel(apiClient)
        {
            BranchIdText = "not-a-guid"
        };

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CallCount);
        Assert.Equal("BranchId must be a valid GUID.", viewModel.ErrorMessage);
    }

    private sealed class RecordingOperatorDiagnosticsApiClient : IOperatorDiagnosticsApiClient
    {
        public int CallCount { get; private set; }

        public Guid LastBranchId { get; private set; }

        public Task<BranchDiagnosticsDto> GetDiagnosticsAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;
            return Task.FromResult(CreateDiagnostics(branchId));
        }

        private static BranchDiagnosticsDto CreateDiagnostics(Guid branchId)
        {
            var staleDeviceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
            var commandId = Guid.Parse("22222222-2222-4222-8222-222222222222");
            var rolloutId = Guid.Parse("33333333-3333-4333-8333-333333333333");

            return new BranchDiagnosticsDto(
                OrganizationId,
                branchId,
                DateTimeOffset.Parse("2026-05-16T10:00:00Z"),
                new DeviceDiagnosticsSummaryDto(
                    TotalDevices: 12,
                    OnlineDevices: 10,
                    LockedDevices: 8,
                    StaleDevices: 2,
                    StaleThresholdSeconds: 300,
                    NewestHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-16T09:59:00Z")),
                new CommandDiagnosticsSummaryDto(
                    PendingCommands: 3,
                    FailedCommands: 1,
                    RecentFailures:
                    [
                        new FailedCommandDiagnosticsDto(
                            staleDeviceId,
                            "PC-STALE",
                            commandId,
                            "unlock",
                            "Failed",
                            "timed out",
                            DateTimeOffset.Parse("2026-05-16T09:58:00Z"))
                    ]),
                new UpdateDiagnosticsSummaryDto(
                    ActiveRollouts: 2,
                    InstallingDevices: 1,
                    FailedDevices: 1,
                    RollbackDevices: 0,
                    RecentFailures:
                    [
                        new FailedUpdateDiagnosticsDto(
                            staleDeviceId,
                            "PC-STALE",
                            rolloutId,
                            "gaming-pc",
                            "0.1.0",
                            "failed",
                            "install failed",
                            DateTimeOffset.Parse("2026-05-16T09:57:00Z"))
                    ]),
                StaleDevices:
                [
                    new StaleDeviceDiagnosticsDto(
                        staleDeviceId,
                        "PC-STALE",
                        "0.1.0",
                        "0.1.0",
                        IsOnline: false,
                        IsLocked: true,
                        LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-16T09:40:00Z"),
                        LastHeartbeatAgeSeconds: 1200)
                ]);
        }
    }
}
