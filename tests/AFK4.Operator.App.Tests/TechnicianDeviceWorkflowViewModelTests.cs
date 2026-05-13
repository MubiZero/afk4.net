using AFK4.Operator.App.Devices;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Tests;

public sealed class TechnicianDeviceWorkflowViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid CommandId = Guid.Parse("b1d839b7-90c8-4158-8668-88e1488cbe5a");
    private static readonly Guid CredentialId = Guid.Parse("12f392fd-8eca-448d-a9fc-e9c95651e83c");

    [Fact]
    public async Task CreateEnrollmentCodeAsync_WithValidInputs_CallsBackendAndExposesCode()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);

        await viewModel.CreateEnrollmentCodeAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastEnrollmentBranchId);
        Assert.Equal(OrganizationId, apiClient.LastEnrollmentOrganizationId);
        Assert.Equal(300, apiClient.LastEnrollmentExpiresInSeconds);
        Assert.Equal("AFK4-TEST-CODE", viewModel.EnrollmentCode);
        Assert.Equal(DateTimeOffset.Parse("2026-05-12T00:05:00Z"), viewModel.EnrollmentCodeExpiresAtUtc);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task DispatchDeviceCommandAsync_WithValidInputs_TracksCommandForStatusInspection()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);
        viewModel.CommandType = "lock";
        viewModel.CommandReason = "maintenance";

        await viewModel.DispatchDeviceCommandAsync(CancellationToken.None);

        Assert.Equal(DeviceId, apiClient.LastCommandDeviceId);
        Assert.Equal("lock", apiClient.LastCommandType);
        Assert.Equal("maintenance", apiClient.LastCommandPayload["reason"]);
        Assert.Equal(CommandId.ToString("D"), viewModel.CommandIdText);
        Assert.Equal("Pending", viewModel.CommandStatus);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshCommandStatusAsync_WithCommandId_UpdatesDisplayedStatus()
    {
        var apiClient = new RecordingOperatorDeviceApiClient
        {
            CommandStatus = "Completed",
            CommandStatusMessage = "Lock applied"
        };
        var viewModel = CreateViewModel(apiClient);
        viewModel.CommandIdText = CommandId.ToString("D");

        await viewModel.RefreshCommandStatusAsync(CancellationToken.None);

        Assert.Equal(DeviceId, apiClient.LastStatusDeviceId);
        Assert.Equal(CommandId, apiClient.LastStatusCommandId);
        Assert.Equal("Completed", viewModel.CommandStatus);
        Assert.Equal("Lock applied", viewModel.CommandStatusMessage);
    }

    [Fact]
    public async Task RotateCredentialAsync_WithDeviceId_ExposesNewCredentialAndSecret()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);

        await viewModel.RotateCredentialAsync(CancellationToken.None);

        Assert.Equal(DeviceId, apiClient.LastRotatedDeviceId);
        Assert.Equal(CredentialId.ToString("D"), viewModel.CredentialIdText);
        Assert.Equal("credential-secret", viewModel.RotatedCredentialSecret);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RevokeCredentialAsync_WithCredentialId_UpdatesRevocationState()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);
        viewModel.CredentialIdText = CredentialId.ToString("D");
        viewModel.RotatedCredentialSecret = "credential-secret";

        await viewModel.RevokeCredentialAsync(CancellationToken.None);

        Assert.Equal(DeviceId, apiClient.LastRevokedDeviceId);
        Assert.Equal(CredentialId, apiClient.LastRevokedCredentialId);
        Assert.Equal("Revoked", viewModel.CredentialStatus);
        Assert.Equal(string.Empty, viewModel.RotatedCredentialSecret);
    }

    [Fact]
    public async Task RefreshDeviceDetailAsync_WithDeviceId_UpdatesDetailState()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);

        await viewModel.RefreshDeviceDetailAsync(CancellationToken.None);

        Assert.Equal(DeviceId, apiClient.LastDetailDeviceId);
        Assert.Equal("PC-001", viewModel.DeviceName);
        Assert.Equal("Seat 01 / Main Hall", viewModel.DeviceSeatSummary);
        Assert.Equal("Online, locked", viewModel.DeviceStateSummary);
        Assert.Equal("Agent 0.1.1 / Shell 0.1.2", viewModel.DeviceVersionSummary);
        Assert.Equal("Active credentials: 1", viewModel.DeviceCredentialSummary);
        Assert.Equal("Installed apps: 2", viewModel.DeviceInstalledAppsSummary);
        Assert.Equal("unlock Completed", viewModel.RecentCommandSummary);
        Assert.Equal("Device PC-001 detail refreshed.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreateEnrollmentCodeAsync_WithInvalidBranchId_DoesNotCallBackendAndShowsError()
    {
        var apiClient = new RecordingOperatorDeviceApiClient();
        var viewModel = CreateViewModel(apiClient);
        viewModel.BranchIdText = "not-a-guid";

        await viewModel.CreateEnrollmentCodeAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.EnrollmentCallCount);
        Assert.Equal("BranchId must be a valid GUID.", viewModel.ErrorMessage);
    }

    private static TechnicianDeviceWorkflowViewModel CreateViewModel(RecordingOperatorDeviceApiClient apiClient)
    {
        return new TechnicianDeviceWorkflowViewModel(apiClient)
        {
            OrganizationIdText = OrganizationId.ToString("D"),
            BranchIdText = BranchId.ToString("D"),
            DeviceIdText = DeviceId.ToString("D"),
            EnrollmentExpiresInSeconds = 300
        };
    }

    private sealed class RecordingOperatorDeviceApiClient : IOperatorDeviceApiClient
    {
        public int EnrollmentCallCount { get; private set; }

        public Guid LastEnrollmentBranchId { get; private set; }

        public Guid LastEnrollmentOrganizationId { get; private set; }

        public int LastEnrollmentExpiresInSeconds { get; private set; }

        public Guid LastCommandDeviceId { get; private set; }

        public string LastCommandType { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, string> LastCommandPayload { get; private set; } =
            new Dictionary<string, string>();

        public Guid LastStatusDeviceId { get; private set; }

        public Guid LastStatusCommandId { get; private set; }

        public string CommandStatus { get; set; } = "Pending";

        public string? CommandStatusMessage { get; set; }

        public Guid LastRotatedDeviceId { get; private set; }

        public Guid LastRevokedDeviceId { get; private set; }

        public Guid LastRevokedCredentialId { get; private set; }

        public Guid LastDetailDeviceId { get; private set; }

        public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
            Guid branchId,
            Guid organizationId,
            int expiresInSeconds,
            CancellationToken cancellationToken)
        {
            EnrollmentCallCount++;
            LastEnrollmentBranchId = branchId;
            LastEnrollmentOrganizationId = organizationId;
            LastEnrollmentExpiresInSeconds = expiresInSeconds;

            return Task.FromResult(new DeviceEnrollmentCodeDto(
                organizationId,
                branchId,
                "AFK4-TEST-CODE",
                DateTimeOffset.Parse("2026-05-12T00:05:00Z")));
        }

        public Task<DeviceCommandDto> DispatchDeviceCommandAsync(
            Guid deviceId,
            string type,
            IReadOnlyDictionary<string, string> payload,
            CancellationToken cancellationToken)
        {
            LastCommandDeviceId = deviceId;
            LastCommandType = type;
            LastCommandPayload = payload;

            return Task.FromResult(new DeviceCommandDto(
                CommandId,
                type,
                DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
                payload));
        }

        public Task<DeviceCommandStatusDto> GetDeviceCommandStatusAsync(
            Guid deviceId,
            Guid commandId,
            CancellationToken cancellationToken)
        {
            LastStatusDeviceId = deviceId;
            LastStatusCommandId = commandId;

            return Task.FromResult(new DeviceCommandStatusDto(
                deviceId,
                commandId,
                "lock",
                CommandStatus,
                CommandStatusMessage,
                DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
                DateTimeOffset.Parse("2026-05-12T00:01:00Z")));
        }

        public Task<RotateDeviceCredentialResponse> RotateDeviceCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            LastRotatedDeviceId = deviceId;

            return Task.FromResult(new RotateDeviceCredentialResponse(
                OrganizationId,
                BranchId,
                deviceId,
                CredentialId,
                "credential-secret",
                DateTimeOffset.Parse("2026-05-12T00:00:00Z")));
        }

        public Task<RevokeDeviceCredentialResponse> RevokeDeviceCredentialAsync(
            Guid deviceId,
            Guid credentialId,
            CancellationToken cancellationToken)
        {
            LastRevokedDeviceId = deviceId;
            LastRevokedCredentialId = credentialId;

            return Task.FromResult(new RevokeDeviceCredentialResponse(
                OrganizationId,
                BranchId,
                deviceId,
                credentialId,
                DateTimeOffset.Parse("2026-05-12T00:01:00Z")));
        }

        public Task<DeviceDetailDto> GetDeviceDetailAsync(Guid deviceId, CancellationToken cancellationToken)
        {
            LastDetailDeviceId = deviceId;

            return Task.FromResult(new DeviceDetailDto(
                OrganizationId,
                BranchId,
                deviceId,
                "PC-001",
                "0.1.1",
                "0.1.2",
                DateTimeOffset.Parse("2026-05-13T09:00:00Z"),
                DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
                IsOnline: true,
                IsLocked: true,
                SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                SeatName: "Seat 01",
                ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
                ZoneName: "Main Hall",
                ActiveCredentialCount: 1,
                InstalledAppCount: 2,
                RecentCommands:
                [
                    new DeviceCommandStatusDto(
                        deviceId,
                        CommandId,
                        "unlock",
                        "Completed",
                        null,
                        DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
                        DateTimeOffset.Parse("2026-05-13T10:01:00Z"))
                ]));
        }
    }
}
