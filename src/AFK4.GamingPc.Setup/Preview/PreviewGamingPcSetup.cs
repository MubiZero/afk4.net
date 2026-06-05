#if DEBUG
using AFK4.GamingPc.Setup.Core;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.GamingPc.Setup.Preview;

/// <summary>
/// Builds a fully wired <see cref="SetupShellViewModel"/> backed by in-memory fakes so the
/// setup window can be opened and clicked through end-to-end without elevation, network calls,
/// disk writes, or touching the Agent service. Launch with: AFK4.GamingPc.Setup.exe --preview
/// </summary>
internal static class PreviewGamingPcSetup
{
    public static SetupShellViewModel CreateViewModel()
    {
        var resources = new SetupEmbeddedResources(typeof(App).Assembly);
        var orchestrator = new GamingPcSetupOrchestrator(
            new FakeApiClient(),
            new FakeMsiInstaller(),
            new FakeConfigurationWriter(),
            new FakeServiceController(),
            TimeProvider.System,
            heartbeatTimeout: TimeSpan.FromSeconds(1),
            heartbeatPollInterval: TimeSpan.FromMilliseconds(100));

        return new SetupShellViewModel(orchestrator, resources);
    }

    private sealed class FakeApiClient : ISetupApiClient
    {
        private static readonly Guid OrgId = new("00000000-0000-0000-0000-0000000000aa");
        private static readonly Guid BranchId = new("00000000-0000-0000-0000-0000000000bb");
        private static readonly Guid DeviceId = new("00000000-0000-0000-0000-0000000000cc");
        private static readonly Guid SeatId = new("00000000-0000-0000-0000-0000000000dd");

        public Task CheckHealthAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<StaffSignInResponse> SignInAsync(string userName, string password, CancellationToken cancellationToken)
            => Task.FromResult(new StaffSignInResponse(
                StaffUserId: Guid.NewGuid(),
                OrganizationId: OrgId,
                DisplayName: "Preview User",
                AccessToken: "preview-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(1),
                RefreshToken: "preview-refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddDays(7),
                BranchIds: [BranchId],
                Permissions: ["preview"]));

        public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(string accessToken, CancellationToken cancellationToken)
            => Task.FromResult(new DeviceEnrollmentCodeDto(
                OrganizationId: OrgId,
                BranchId: BranchId,
                Code: "PREVIEW-CODE",
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(5)));

        public Task<DeviceEnrollmentResponse> EnrollDeviceAsync(string enrollmentCode, string machineName, CancellationToken cancellationToken)
            => Task.FromResult(new DeviceEnrollmentResponse(
                OrganizationId: OrgId,
                BranchId: BranchId,
                DeviceId: DeviceId,
                CredentialId: Guid.NewGuid(),
                CredentialSecret: "preview-secret",
                EnrolledAtUtc: DateTimeOffset.UtcNow));

        public Task<DeviceSeatAssignmentDto> AssignDeviceToSmokeSeatAsync(string accessToken, Guid deviceId, CancellationToken cancellationToken)
            => Task.FromResult(new DeviceSeatAssignmentDto(
                DeviceSeatAssignmentId: Guid.NewGuid(),
                OrganizationId: OrgId,
                BranchId: BranchId,
                SeatId: SeatId,
                DeviceId: deviceId,
                AttachedAtUtc: DateTimeOffset.UtcNow,
                DetachedAtUtc: null));

        public Task<DeviceDetailDto> GetDeviceDetailAsync(string accessToken, Guid deviceId, CancellationToken cancellationToken)
            => Task.FromResult(new DeviceDetailDto(
                OrganizationId: OrgId,
                BranchId: BranchId,
                DeviceId: deviceId,
                MachineName: "PREVIEW-PC",
                AgentVersion: "1.0.0-preview",
                ShellVersion: "1.0.0-preview",
                EnrolledAtUtc: DateTimeOffset.UtcNow,
                LastHeartbeatAtUtc: DateTimeOffset.UtcNow,
                IsOnline: true,
                IsLocked: false,
                SeatId: SeatId,
                SeatName: "PC-01",
                ZoneId: Guid.NewGuid(),
                ZoneName: "Main Hall",
                ActiveCredentialCount: 1,
                InstalledAppCount: 0,
                RecentCommands: []));
    }

    private sealed class FakeMsiInstaller : IGamingPcMsiInstaller
    {
        public Task InstallAsync(CancellationToken cancellationToken)
        {
            // no-op: preview must not touch the machine
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigurationWriter : IAgentMachineConfigurationWriter
    {
        public void Write(SetupRequest request, DeviceEnrollmentResponse enrollment)
        {
            // no-op: preview must not write config files to disk
        }
    }

    private sealed class FakeServiceController : IWindowsServiceController
    {
        public void StopIfExists(string serviceName)
        {
            // no-op: preview must not touch Windows services
        }

        public void Start(string serviceName)
        {
            // no-op: preview must not touch Windows services
        }

        public string QueryStatus(string serviceName) => "Running";
    }
}
#endif
