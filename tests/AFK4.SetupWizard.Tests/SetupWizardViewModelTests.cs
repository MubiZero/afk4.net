using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class SetupWizardViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid FreeSeatId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OccupiedSeatId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task DiscoverAsync_NormalizesPastedOwnerCodeAndLoadsBranches()
    {
        var apiClient = new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse()
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.OwnerCode = "12 34-5678";

        await viewModel.DiscoverAsync(CancellationToken.None);

        Assert.Equal("12345678", apiClient.DiscoverOwnerCode);
        Assert.Equal(SetupWizardStep.BranchSelection, viewModel.CurrentStep);
        var branch = Assert.Single(viewModel.Branches);
        Assert.Equal("Demo Branch", branch.Name);
        Assert.Equal("Owner: Tech Owner", viewModel.StatusMessage);
    }

    [Fact]
    public async Task DiscoverAsync_WithInvalidOwnerCodeDoesNotCallApi()
    {
        var apiClient = new RecordingSetupWizardApiClient();
        var viewModel = CreateViewModel(apiClient);
        viewModel.OwnerCode = "1234";

        await viewModel.DiscoverAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.DiscoverCallCount);
        Assert.Equal(SetupWizardStep.OwnerCode, viewModel.CurrentStep);
        Assert.Equal("Owner code must be exactly 8 digits.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task DiscoverAsync_WithAmbiguousOwnerCodeCharactersDoesNotCallApi()
    {
        var apiClient = new RecordingSetupWizardApiClient();
        var viewModel = CreateViewModel(apiClient);
        viewModel.OwnerCode = "12AB345678";

        await viewModel.DiscoverAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.DiscoverCallCount);
        Assert.Equal("Owner code must contain only digits, spaces, or dashes.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SelectBranch_LoadsSeatsAndMovesToRoleSelection()
    {
        var viewModel = CreateViewModel(new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse()
        });
        viewModel.OwnerCode = "12345678";
        await viewModel.DiscoverAsync(CancellationToken.None);

        viewModel.SelectBranch(viewModel.Branches.Single());

        Assert.Equal(SetupWizardStep.RoleSelection, viewModel.CurrentStep);
        Assert.Equal(2, viewModel.Seats.Count);
        var free = Assert.Single(viewModel.Seats, seat => seat.SeatId == FreeSeatId);
        var occupied = Assert.Single(viewModel.Seats, seat => seat.SeatId == OccupiedSeatId);
        Assert.True(free.IsSelectable);
        Assert.False(occupied.IsSelectable);
    }

    [Fact]
    public async Task ContinueFromRole_WithGamingPcMovesToSeatSelection()
    {
        var viewModel = CreateViewModel(new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse()
        });
        viewModel.OwnerCode = "12345678";
        await viewModel.DiscoverAsync(CancellationToken.None);
        viewModel.SelectBranch(viewModel.Branches.Single());

        viewModel.SelectedRole = DeviceRoleNames.GamingPc;
        viewModel.ContinueFromRole();

        Assert.Equal(SetupWizardStep.SeatSelection, viewModel.CurrentStep);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreateSeatAsync_AddsReturnedSeatAsFreeAndSelectsIt()
    {
        var apiClient = new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse(),
            CreateSeatResponse = new InstallCreateSeatResponse(
                OrganizationId,
                BranchId,
                ZoneId,
                Guid.Parse("44444444-4444-4444-8444-444444444444"),
                "PC-NEW",
                SortOrder: 3)
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.OwnerCode = "12345678";
        await viewModel.DiscoverAsync(CancellationToken.None);
        viewModel.SelectBranch(viewModel.Branches.Single());

        await viewModel.CreateSeatAsync("PC-NEW", CancellationToken.None);

        Assert.Equal("12345678", apiClient.CreateSeatOwnerCode);
        Assert.Equal(BranchId, apiClient.CreateSeatBranchId);
        Assert.Equal(ZoneId, apiClient.CreateSeatZoneId);
        Assert.Equal("PC-NEW", apiClient.CreateSeatName);
        Assert.Equal(apiClient.CreateSeatResponse!.SeatId, viewModel.SelectedSeat!.SeatId);
        Assert.Contains(viewModel.Seats, seat => seat.SeatId == apiClient.CreateSeatResponse.SeatId && seat.IsSelectable);
    }

    [Fact]
    public async Task EnrollAsync_ManagerWorkstationUsesBranchOnlyAndWritesBootstrap()
    {
        var apiClient = new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse(),
            EnrollResponse = new InstallEnrollResponse(
                OrganizationId,
                BranchId,
                Guid.Parse("55555555-5555-4555-8555-555555555555"),
                Guid.Parse("66666666-6666-4666-8666-666666666666"),
                "credential-secret",
                DeviceEnrollmentStateNames.Pending,
                "https://afk4.staging.mubi.dev",
                "internal",
                DateTimeOffset.Parse("2026-05-25T12:00:00Z"))
            {
                LeaseSigningPublicKeyPem = "lease-public-key",
                UpdatePackageSigningPublicKeyPem = "update-public-key"
            }
        };
        var keyStore = new RecordingDeviceKeyStore("device-public-key");
        var bootstrapWriter = new RecordingBootstrapWriter();
        var completionAction = new RecordingCompletionAction();
        var viewModel = CreateViewModel(apiClient, keyStore, bootstrapWriter, completionAction);
        viewModel.OwnerCode = "12345678";
        viewModel.DisplayName = "Manager desk";
        viewModel.SelectedRole = DeviceRoleNames.ManagerWorkstation;
        await viewModel.DiscoverAsync(CancellationToken.None);
        viewModel.SelectBranch(viewModel.Branches.Single());

        await viewModel.EnrollAsync(CancellationToken.None);

        Assert.Equal("12345678", apiClient.EnrollRequest!.OwnerCode);
        Assert.Equal(BranchId, apiClient.EnrollRequest.BranchId);
        Assert.Null((Guid?)apiClient.EnrollRequest.SeatId);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, apiClient.EnrollRequest.Role);
        Assert.Equal("Manager desk", apiClient.EnrollRequest.DisplayName);
        Assert.Equal(Environment.MachineName, apiClient.EnrollRequest.MachineName);
        Assert.Equal("device-public-key", apiClient.EnrollRequest.DevicePublicKey);
        Assert.Equal(1, keyStore.CallCount);
        Assert.NotNull(bootstrapWriter.Config);
        Assert.Equal(apiClient.EnrollResponse.DeviceId, bootstrapWriter.Config.DeviceId);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, bootstrapWriter.Config.Role);
        Assert.Equal(1, completionAction.CallCount);
        Assert.Equal("Enrollment pending approval in the club dashboard.", viewModel.StatusMessage);
        Assert.Equal(SetupWizardStep.Finished, viewModel.CurrentStep);
    }

    [Fact]
    public async Task EnrollAsync_GamingPcWithoutSeatRequiresSeatBeforeApiCall()
    {
        var apiClient = new RecordingSetupWizardApiClient
        {
            DiscoverResponse = CreateDiscoverResponse()
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.OwnerCode = "12345678";
        viewModel.SelectedRole = DeviceRoleNames.GamingPc;
        await viewModel.DiscoverAsync(CancellationToken.None);
        viewModel.SelectBranch(viewModel.Branches.Single());

        await viewModel.EnrollAsync(CancellationToken.None);

        Assert.Null(apiClient.EnrollRequest);
        Assert.Equal(SetupWizardStep.SeatSelection, viewModel.CurrentStep);
        Assert.Equal("Choose a free seat before enrolling a gaming PC.", viewModel.ErrorMessage);
    }

    private static SetupWizardViewModel CreateViewModel(
        RecordingSetupWizardApiClient apiClient,
        IDeviceKeyStore? keyStore = null,
        ISetupWizardBootstrapWriter? bootstrapWriter = null,
        ISetupWizardCompletionAction? completionAction = null) =>
        new(
            apiClient,
            keyStore ?? new RecordingDeviceKeyStore("device-public-key"),
            bootstrapWriter ?? new RecordingBootstrapWriter(),
            new SetupWizardMachineInfo(Environment.MachineName),
            completionAction);

    private static InstallDiscoverResponse CreateDiscoverResponse()
    {
        var floorMap = new FloorMapDto(
            BranchId,
            "Demo Branch",
            [
                new SeatStatusDto(
                    FreeSeatId,
                    "PC-001",
                    ZoneId,
                    "Main Hall",
                    SortOrder: 1,
                    "Free",
                    DeviceId: null,
                    DeviceName: null,
                    IsDeviceOnline: null,
                    IsDeviceLocked: null,
                    LastHeartbeatAtUtc: null,
                    AgentVersion: null,
                    ShellVersion: null,
                    ActiveSessionId: null,
                    RemainingSeconds: null),
                new SeatStatusDto(
                    OccupiedSeatId,
                    "PC-002",
                    ZoneId,
                    "Main Hall",
                    SortOrder: 2,
                    "Maintenance",
                    Guid.Parse("77777777-7777-4777-8777-777777777777"),
                    "PC-002",
                    IsDeviceOnline: false,
                    IsDeviceLocked: true,
                    LastHeartbeatAtUtc: null,
                    AgentVersion: "0.1.0",
                    ShellVersion: "0.1.0",
                    ActiveSessionId: null,
                    RemainingSeconds: null)
            ])
        {
            Zones = [new FloorMapZoneDto(ZoneId, "Main Hall", SortOrder: 1)]
        };

        return new InstallDiscoverResponse(
            "Tech Owner",
            [new InstallBranchDto(BranchId, "demo", "Demo Branch", floorMap, [FreeSeatId])]);
    }

    private sealed class RecordingSetupWizardApiClient : ISetupWizardApiClient
    {
        public int DiscoverCallCount { get; private set; }

        public string? DiscoverOwnerCode { get; private set; }

        public InstallDiscoverResponse DiscoverResponse { get; init; } = new("Owner", []);

        public string? CreateSeatOwnerCode { get; private set; }

        public Guid? CreateSeatBranchId { get; private set; }

        public Guid? CreateSeatZoneId { get; private set; }

        public string? CreateSeatName { get; private set; }

        public InstallCreateSeatResponse? CreateSeatResponse { get; init; }

        public InstallEnrollRequest? EnrollRequest { get; private set; }

        public InstallEnrollResponse EnrollResponse { get; init; } = new(
            OrganizationId,
            BranchId,
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            "credential-secret",
            DeviceEnrollmentStateNames.Approved,
            "https://afk4.staging.mubi.dev",
            "internal",
            DateTimeOffset.Parse("2026-05-25T12:00:00Z"));

        public Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken)
        {
            DiscoverCallCount++;
            DiscoverOwnerCode = ownerCode;
            return Task.FromResult(DiscoverResponse);
        }

        public Task<InstallCreateSeatResponse> CreateSeatAsync(
            string ownerCode,
            Guid branchId,
            Guid zoneId,
            string name,
            CancellationToken cancellationToken)
        {
            CreateSeatOwnerCode = ownerCode;
            CreateSeatBranchId = branchId;
            CreateSeatZoneId = zoneId;
            CreateSeatName = name;
            return Task.FromResult(CreateSeatResponse!);
        }

        public Task<InstallEnrollResponse> EnrollAsync(
            InstallEnrollRequest request,
            CancellationToken cancellationToken)
        {
            EnrollRequest = request;
            return Task.FromResult(EnrollResponse);
        }

        public Task<StaffSignInResponse> SignInByPhoneAsync(string phoneNumber, string password, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(string accessToken, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class RecordingDeviceKeyStore(string publicKeyPem) : IDeviceKeyStore
    {
        public int CallCount { get; private set; }

        public Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(publicKeyPem);
        }
    }

    private sealed class RecordingBootstrapWriter : ISetupWizardBootstrapWriter
    {
        public SetupWizardBootstrapConfig? Config { get; private set; }

        public void Write(SetupWizardBootstrapConfig config)
        {
            Config = config;
        }
    }

    private sealed class RecordingCompletionAction : ISetupWizardCompletionAction
    {
        public int CallCount { get; private set; }

        public void Complete()
        {
            CallCount++;
        }
    }
}
