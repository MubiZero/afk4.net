using AFK4.Operator.App.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Tests;

public sealed class UpdateStatusWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid RolloutId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PackageId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");

    [Fact]
    public async Task RefreshAsync_WithBranchContext_LoadsRolloutStatusSummary()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient);
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastBranchId);
        var rollout = Assert.Single(viewModel.Rollouts);
        Assert.Equal("AgentService 1.2.3", rollout.Title);
        Assert.Equal("Branch / 100% batch", rollout.TargetSummary);
        Assert.Equal("1 installed, 0 failed, 1 reporting", rollout.ProgressSummary);
        Assert.Equal(UpdateStatusNames.Installed, rollout.DeviceStatuses.Single().Status);
        Assert.Equal("1 update rollout loaded.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidBranchId_DoesNotCallBackendAndShowsError()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient)
        {
            BranchIdText = "not-a-guid"
        };

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CallCount);
        Assert.Equal("BranchId must be a valid GUID.", viewModel.ErrorMessage);
    }

    private sealed class RecordingOperatorUpdateApiClient : IOperatorUpdateApiClient
    {
        public int CallCount { get; private set; }

        public Guid LastBranchId { get; private set; }

        public Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;

            return Task.FromResult<IReadOnlyList<UpdateRolloutStatusDto>>(
            [
                new UpdateRolloutStatusDto(
                    RolloutId,
                    OrganizationId,
                    branchId,
                    PackageId,
                    UpdateComponentNames.AgentService,
                    "1.2.3",
                    UpdateChannelNames.Beta,
                    UpdateRolloutStateNames.Active,
                    UpdateTargetKindNames.Branch,
                    [],
                    BatchPercent: 100,
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
                    StartsAtUtc: DateTimeOffset.Parse("2026-05-14T12:30:00Z"),
                    CompletedAtUtc: null,
                    DeviceStatuses:
                    [
                        new DeviceUpdateStatusSnapshotDto(
                            DeviceId,
                            RolloutId,
                            PackageId,
                            UpdateComponentNames.AgentService,
                            "1.2.2",
                            "1.2.3",
                            UpdateStatusNames.Installed,
                            "installed",
                            DateTimeOffset.Parse("2026-05-14T12:45:00Z"))
                    ])
            ]);
        }
    }
}
