using AFK4.GamingPc.Setup.Core;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.GamingPc.Setup.Tests;

public sealed class GamingPcSetupOrchestratorTests
{
    [Fact]
    public async Task InstallAsync_CompletesAllSteps_WhenHeartbeatIsObserved()
    {
        var api = new FakeSetupApiClient
        {
            DeviceDetail = CreateDeviceDetail(isOnline: true, lastHeartbeatAtUtc: DateTimeOffset.UtcNow)
        };
        var msi = new RecordingMsiInstaller();
        var config = new RecordingConfigurationWriter();
        var service = new RecordingServiceController();
        var orchestrator = CreateOrchestrator(api, msi, config, service);

        var result = await orchestrator.InstallAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(api.Enrollment.DeviceId, result.DeviceId);
        Assert.True(msi.WasInstalled);
        Assert.True(config.WasWritten);
        Assert.Contains(StagingSetupDefaults.AgentServiceName, service.StartedServices);
        Assert.Equal(
            ["Health", "SignIn", "EnrollmentCode", "Enroll", "InstallMsi", "ConfigureAgent", "StartService", "Heartbeat"],
            result.Steps.Where(step => step.Status is SetupStepStatus.Succeeded).Select(step => step.Step).ToArray());
    }

    [Fact]
    public async Task InstallAsync_StopsBeforeEnrollmentAndInstall_WhenSignInFails()
    {
        var api = new FakeSetupApiClient { SignInException = new InvalidOperationException("bad credentials") };
        var msi = new RecordingMsiInstaller();
        var config = new RecordingConfigurationWriter();
        var orchestrator = CreateOrchestrator(api, msi, config, new RecordingServiceController());

        var result = await orchestrator.InstallAsync(CreateRequest());

        Assert.Equal(SetupStepStatus.Failed, result.Status);
        Assert.False(msi.WasInstalled);
        Assert.False(config.WasWritten);
        Assert.Contains(result.Steps, step => step.Step == "SignIn" && step.Status == SetupStepStatus.Failed);
        Assert.DoesNotContain(result.Steps, step => step.Step == "Enroll");
    }

    [Fact]
    public async Task InstallAsync_StopsBeforeConfiguration_WhenMsiInstallFails()
    {
        var api = new FakeSetupApiClient();
        var msi = new RecordingMsiInstaller { Exception = new InvalidOperationException("msi failed") };
        var config = new RecordingConfigurationWriter();
        var orchestrator = CreateOrchestrator(api, msi, config, new RecordingServiceController());

        var result = await orchestrator.InstallAsync(CreateRequest());

        Assert.Equal(SetupStepStatus.Failed, result.Status);
        Assert.False(config.WasWritten);
        Assert.Contains(result.Steps, step => step.Step == "InstallMsi" && step.Status == SetupStepStatus.Failed);
        Assert.DoesNotContain(result.Steps, step => step.Step == "ConfigureAgent");
    }

    [Fact]
    public async Task InstallAsync_ReportsServiceFailure_WhenServiceCannotStart()
    {
        var service = new RecordingServiceController { StartException = new InvalidOperationException("service failed") };
        var orchestrator = CreateOrchestrator(
            new FakeSetupApiClient(),
            new RecordingMsiInstaller(),
            new RecordingConfigurationWriter(),
            service);

        var result = await orchestrator.InstallAsync(CreateRequest());

        Assert.Equal(SetupStepStatus.Failed, result.Status);
        Assert.Contains(result.Steps, step => step.Step == "StartService" && step.Status == SetupStepStatus.Failed);
        Assert.DoesNotContain(result.Steps, step => step.Step == "Heartbeat");
    }

    [Fact]
    public async Task InstallAsync_ReturnsPartial_WhenHeartbeatIsNotObserved()
    {
        var api = new FakeSetupApiClient
        {
            DeviceDetail = CreateDeviceDetail(isOnline: false, lastHeartbeatAtUtc: null)
        };
        var orchestrator = CreateOrchestrator(
            api,
            new RecordingMsiInstaller(),
            new RecordingConfigurationWriter(),
            new RecordingServiceController(),
            heartbeatTimeout: TimeSpan.Zero);

        var result = await orchestrator.InstallAsync(CreateRequest());

        Assert.True(result.IsPartial);
        Assert.Contains(result.Steps, step => step.Step == "Heartbeat" && step.Status == SetupStepStatus.Partial);
    }

    private static GamingPcSetupOrchestrator CreateOrchestrator(
        ISetupApiClient apiClient,
        IGamingPcMsiInstaller msiInstaller,
        IAgentMachineConfigurationWriter configurationWriter,
        IWindowsServiceController serviceController,
        TimeSpan? heartbeatTimeout = null) =>
        new(
            apiClient,
            msiInstaller,
            configurationWriter,
            serviceController,
            TimeProvider.System,
            heartbeatTimeout,
            TimeSpan.FromMilliseconds(1));

    private static SetupRequest CreateRequest() =>
        new("operator@afk4.test", "password", "VM-AFK4-001", "-----BEGIN PUBLIC KEY-----\nkey\n-----END PUBLIC KEY-----");

    private static DeviceDetailDto CreateDeviceDetail(bool isOnline, DateTimeOffset? lastHeartbeatAtUtc) =>
        new(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.BranchId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "VM-AFK4-001",
            StagingSetupDefaults.AgentVersion,
            StagingSetupDefaults.ShellVersion,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            lastHeartbeatAtUtc,
            isOnline,
            IsLocked: true,
            SeatId: null,
            SeatName: null,
            ZoneId: null,
            ZoneName: null,
            ActiveCredentialCount: 1,
            InstalledAppCount: 3,
            RecentCommands: []);

    private sealed class FakeSetupApiClient : ISetupApiClient
    {
        public Exception? SignInException { get; init; }

        public DeviceEnrollmentResponse Enrollment { get; } = new(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.BranchId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "device-secret",
            DateTimeOffset.UtcNow);

        public DeviceDetailDto DeviceDetail { get; init; } = CreateDeviceDetail(
            isOnline: true,
            lastHeartbeatAtUtc: DateTimeOffset.UtcNow);

        public Task CheckHealthAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StaffSignInResponse> SignInAsync(string userName, string password, CancellationToken cancellationToken)
        {
            if (SignInException is not null)
            {
                throw SignInException;
            }

            return Task.FromResult(new StaffSignInResponse(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                StagingSetupDefaults.OrganizationId,
                "Smoke Operator",
                "access-token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTimeOffset.UtcNow.AddHours(8),
                [StagingSetupDefaults.BranchId],
                [StaffPermissionNames.CreateDeviceEnrollmentCode]));
        }

        public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceEnrollmentCodeDto(
                StagingSetupDefaults.OrganizationId,
                StagingSetupDefaults.BranchId,
                "ABC123",
                DateTimeOffset.UtcNow.AddMinutes(5)));

        public Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
            string enrollmentCode,
            string machineName,
            CancellationToken cancellationToken) =>
            Task.FromResult(Enrollment);

        public Task<DeviceDetailDto> GetDeviceDetailAsync(
            string accessToken,
            Guid deviceId,
            CancellationToken cancellationToken) =>
            Task.FromResult(DeviceDetail);
    }

    private sealed class RecordingMsiInstaller : IGamingPcMsiInstaller
    {
        public bool WasInstalled { get; private set; }

        public Exception? Exception { get; init; }

        public Task InstallAsync(CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            WasInstalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingConfigurationWriter : IAgentMachineConfigurationWriter
    {
        public bool WasWritten { get; private set; }

        public void Write(SetupRequest request, DeviceEnrollmentResponse enrollment)
        {
            WasWritten = true;
        }
    }

    private sealed class RecordingServiceController : IWindowsServiceController
    {
        public List<string> StartedServices { get; } = [];

        public Exception? StartException { get; init; }

        public void StopIfExists(string serviceName)
        {
        }

        public void Start(string serviceName)
        {
            if (StartException is not null)
            {
                throw StartException;
            }

            StartedServices.Add(serviceName);
        }

        public string QueryStatus(string serviceName) => "RUNNING";
    }
}
