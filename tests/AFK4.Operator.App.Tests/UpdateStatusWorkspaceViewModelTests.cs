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

    [Fact]
    public async Task RegisterPackageAsync_WithValidForm_CallsBackendAndPrefillsRolloutInputs()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient)
        {
            ComponentText = UpdateComponentNames.AgentService,
            VersionText = "1.2.4",
            ChannelText = UpdateChannelNames.Beta,
            ArtifactUriText = "https://cdn.afk4.test/agent-service/beta/1.2.4/agent.zip",
            Sha256Text = new string('b', 64),
            SignatureText = "package-signature",
            SignatureAlgorithmText = UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            SizeBytesText = "4096",
            ReleaseNotesText = "Beta Agent update."
        };
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.RegisterPackageAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastBranchId);
        Assert.NotNull(apiClient.LastCreatePackageRequest);
        Assert.Equal(OrganizationId, apiClient.LastCreatePackageRequest.OrganizationId);
        Assert.Equal("1.2.4", apiClient.LastCreatePackageRequest.Version);
        Assert.Equal("https://cdn.afk4.test/agent-service/beta/1.2.4/agent.zip", apiClient.LastCreatePackageRequest.ArtifactUri);
        Assert.Equal(PackageId.ToString("D"), viewModel.LastPackageIdText);
        Assert.Equal(PackageId.ToString("D"), viewModel.RolloutPackageIdText);
        Assert.Equal(PackageId.ToString("D"), viewModel.PackageStatePackageIdText);
        Assert.Equal("Registered update package AgentService 1.2.4.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreateRolloutAsync_WithBranchTarget_CallsBackendAndStoresRolloutId()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient)
        {
            RolloutPackageIdText = PackageId.ToString("D"),
            RolloutChannelText = UpdateChannelNames.Beta,
            TargetKindText = UpdateTargetKindNames.Branch,
            TargetDeviceIdsText = string.Empty,
            BatchPercentText = "25",
            StartsAtUtcText = "2026-05-14T13:00:00Z",
            RolloutReasonText = "Canary rollout."
        };
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.CreateRolloutAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastBranchId);
        Assert.NotNull(apiClient.LastCreateRolloutRequest);
        Assert.Equal(PackageId, apiClient.LastCreateRolloutRequest.UpdatePackageId);
        Assert.Equal(UpdateTargetKindNames.Branch, apiClient.LastCreateRolloutRequest.TargetKind);
        Assert.Empty(apiClient.LastCreateRolloutRequest.TargetDeviceIds);
        Assert.Equal(25, apiClient.LastCreateRolloutRequest.BatchPercent);
        Assert.Equal(RolloutId.ToString("D"), viewModel.LastRolloutIdText);
        Assert.Equal(RolloutId.ToString("D"), viewModel.RolloutStateRolloutIdText);
        Assert.Equal("Created update rollout AgentService 1.2.3.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ChangePackageStateAsync_WithValidInputs_CallsBackend()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient)
        {
            PackageStatePackageIdText = PackageId.ToString("D"),
            PackageStateText = UpdatePackageStateNames.Validated,
            PackageStateReasonText = "Signature verified."
        };
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.ChangePackageStateAsync(CancellationToken.None);

        Assert.Equal(PackageId, apiClient.LastPackageStatePackageId);
        Assert.NotNull(apiClient.LastPackageStateChangeRequest);
        Assert.Equal(UpdatePackageStateNames.Validated, apiClient.LastPackageStateChangeRequest.State);
        Assert.Equal("Signature verified.", apiClient.LastPackageStateChangeRequest.Reason);
        Assert.Equal("Package state changed to validated.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ChangeRolloutStateAsync_WithValidInputs_CallsBackend()
    {
        var apiClient = new RecordingOperatorUpdateApiClient();
        var viewModel = new UpdateStatusWorkspaceViewModel(apiClient)
        {
            RolloutStateRolloutIdText = RolloutId.ToString("D"),
            RolloutStateText = UpdateRolloutStateNames.Paused,
            RolloutStateReasonText = "Pause while checking failures."
        };
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.ChangeRolloutStateAsync(CancellationToken.None);

        Assert.Equal(RolloutId, apiClient.LastRolloutStateRolloutId);
        Assert.NotNull(apiClient.LastRolloutStateChangeRequest);
        Assert.Equal(UpdateRolloutStateNames.Paused, apiClient.LastRolloutStateChangeRequest.State);
        Assert.Equal("Pause while checking failures.", apiClient.LastRolloutStateChangeRequest.Reason);
        Assert.Equal("Rollout state changed to paused.", viewModel.StatusMessage);
    }

    private sealed class RecordingOperatorUpdateApiClient : IOperatorUpdateApiClient
    {
        public int CallCount { get; private set; }

        public Guid LastBranchId { get; private set; }

        public CreateUpdatePackageRequest? LastCreatePackageRequest { get; private set; }

        public CreateUpdateRolloutRequest? LastCreateRolloutRequest { get; private set; }

        public Guid LastPackageStatePackageId { get; private set; }

        public UpdatePackageStateChangeRequest? LastPackageStateChangeRequest { get; private set; }

        public Guid LastRolloutStateRolloutId { get; private set; }

        public UpdateRolloutStateChangeRequest? LastRolloutStateChangeRequest { get; private set; }

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

        public Task<UpdatePackageDto> RegisterPackageAsync(
            Guid branchId,
            CreateUpdatePackageRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;
            LastCreatePackageRequest = request;
            return Task.FromResult(new UpdatePackageDto(
                PackageId,
                request.OrganizationId,
                branchId,
                request.Component,
                request.Version,
                request.Channel,
                request.ArtifactUri,
                request.Sha256,
                request.Signature,
                request.SignatureAlgorithm,
                request.SizeBytes,
                UpdatePackageStateNames.Registered,
                request.ReleaseNotes,
                DateTimeOffset.Parse("2026-05-14T12:15:00Z")));
        }

        public Task<UpdatePackageDto> ChangePackageStateAsync(
            Guid branchId,
            Guid updatePackageId,
            UpdatePackageStateChangeRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;
            LastPackageStatePackageId = updatePackageId;
            LastPackageStateChangeRequest = request;
            return Task.FromResult(new UpdatePackageDto(
                updatePackageId,
                request.OrganizationId,
                branchId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                UpdateChannelNames.Beta,
                "https://cdn.afk4.test/agent-service/beta/1.2.3/agent.zip",
                new string('a', 64),
                "signature",
                UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
                SizeBytes: 4096,
                request.State,
                "Beta Agent update.",
                DateTimeOffset.Parse("2026-05-14T12:15:00Z")));
        }

        public Task<UpdateRolloutDto> CreateRolloutAsync(
            Guid branchId,
            CreateUpdateRolloutRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;
            LastCreateRolloutRequest = request;
            return Task.FromResult(new UpdateRolloutDto(
                RolloutId,
                request.OrganizationId,
                branchId,
                request.UpdatePackageId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                request.Channel,
                UpdateRolloutStateNames.Active,
                request.TargetKind,
                request.TargetDeviceIds,
                request.BatchPercent,
                DateTimeOffset.Parse("2026-05-14T12:45:00Z"),
                request.StartsAtUtc,
                CompletedAtUtc: null));
        }

        public Task<UpdateRolloutDto> ChangeRolloutStateAsync(
            Guid branchId,
            Guid updateRolloutId,
            UpdateRolloutStateChangeRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastBranchId = branchId;
            LastRolloutStateRolloutId = updateRolloutId;
            LastRolloutStateChangeRequest = request;
            return Task.FromResult(new UpdateRolloutDto(
                updateRolloutId,
                request.OrganizationId,
                branchId,
                PackageId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                UpdateChannelNames.Beta,
                request.State,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 25,
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:45:00Z"),
                StartsAtUtc: DateTimeOffset.Parse("2026-05-14T13:00:00Z"),
                CompletedAtUtc: null));
        }
    }
}
