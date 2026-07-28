#if DEBUG
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Preview;

/// <summary>
/// Fake services so the wizard can be launched end-to-end without elevation, network calls,
/// disk writes, or touching the Agent service. Launch with: AFK4.SetupWizard.exe --preview
/// </summary>
internal static class PreviewSetupWizard
{
    public static ISetupWizardApiClient CreateApiClient() => new FakeApiClient();

    public static IDeviceKeyStore CreateDeviceKeyStore() => new FakeDeviceKeyStore();

    public static ISetupWizardBootstrapWriter CreateBootstrapWriter() => new FakeBootstrapWriter();

    public static ISetupWizardCompletionAction CreateCompletionAction() => new FakeCompletionAction();

    public static ISetupWizardShellProvisioner CreateShellProvisioner() =>
        new PreviewShellProvisioner();

    public static ISetupWizardOperatorLauncher CreateOperatorLauncher() =>
        new FakeOperatorLauncher();

    private sealed class FakeOperatorLauncher : ISetupWizardOperatorLauncher
    {
        public void Launch()
        {
            // preview must not start the operator app
        }
    }

    private sealed class PreviewShellProvisioner : ISetupWizardShellProvisioner
    {
        public ShellProvisionResult Provision() => ShellProvisionResult.Installed(0);
    }

    private sealed class FakeApiClient : ISetupWizardApiClient
    {
        public Task<StaffSignInResponse> SignInByPhoneAsync(string phoneNumber, string password, CancellationToken cancellationToken)
            => Task.FromResult(PreviewSignIn("Preview Staff"));

        private static StaffSignInResponse PreviewSignIn(string displayName)
            => new(
                StaffUserId: Guid.NewGuid(),
                OrganizationId: OrgId,
                DisplayName: displayName,
                AccessToken: "preview-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(8),
                RefreshToken: "preview-refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.UtcNow.AddDays(30),
                BranchIds: [BranchId],
                Permissions: [OrganizationPermissionNames.InstallDevice]);

        public Task<SetupWizardLoginResult> SignInByLoginAsync(string login, string password, CancellationToken cancellationToken)
            => Task.FromResult(new SetupWizardLoginResult(SignedIn: PreviewSignIn("Preview Staff"), Clubs: []));

        public Task<StaffSignInResponse> SignInToClubAsync(Guid organizationId, string login, string password, CancellationToken cancellationToken)
            => Task.FromResult(PreviewSignIn("Preview Staff"));

        public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ResetPasswordByEmailAsync(string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(string accessToken, CancellationToken cancellationToken)
            => Task.FromResult(new InstallDiscoverResponse("Preview Staff", [BuildBranch()]));

        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
            => Task.FromResult(new InstallCreateSeatResponse(
                OrgId, branchId, zoneId, Guid.NewGuid(), name, SortOrder: 99));

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new InstallEnrollResponse(
                OrgId,
                request.BranchId,
                DeviceId: Guid.NewGuid(),
                CredentialId: Guid.NewGuid(),
                CredentialSecret: "preview-secret",
                EnrollmentState: DeviceEnrollmentStateNames.Approved,
                ApiBaseUrl: "https://preview.local",
                UpdateChannel: "stable",
                EnrolledAtUtc: DateTimeOffset.UnixEpoch));

        private static readonly Guid OrgId = new("00000000-0000-0000-0000-0000000000aa");
        private static readonly Guid BranchId = new("00000000-0000-0000-0000-0000000000bb");
        private static readonly Guid MainHallZoneId = new("00000000-0000-0000-0000-0000000000cc");
        private static readonly Guid VipZoneId = new("00000000-0000-0000-0000-0000000000dd");

        private static InstallBranchDto BuildBranch()
        {
            var mainHall = Enumerable.Range(1, 22)
                .Select(i => MakeSeat($"PC-{i:D2}", i, MainHallZoneId, "Общий зал", isOccupied: i % 3 == 0))
                .ToList();
            var vip = Enumerable.Range(1, 6)
                .Select(i => MakeSeat($"VIP-{i:D2}", i, VipZoneId, "VIP", isOccupied: i % 2 == 0))
                .ToList();
            var seats = mainHall.Concat(vip).ToArray();
            var freeSeatIds = seats.Where(s => s.State == "Free").Select(s => s.SeatId).ToArray();
            var floorMap = new FloorMapDto(BranchId, "Preview Club", seats)
            {
                Zones =
                [
                    new FloorMapZoneDto(MainHallZoneId, "Общий зал", 1),
                    new FloorMapZoneDto(VipZoneId, "VIP", 2),
                ],
            };
            return new InstallBranchDto(BranchId, "preview-club", "Preview Club", floorMap, freeSeatIds);
        }

        private static SeatStatusDto MakeSeat(string name, int order, Guid zoneId, string zoneName, bool isOccupied) =>
            new(
                Guid.NewGuid(),
                name,
                zoneId,
                zoneName,
                order,
                isOccupied ? "Occupied" : "Free",
                isOccupied ? Guid.NewGuid() : null,
                isOccupied ? name : null,
                isOccupied ? (bool?)true : null,
                isOccupied ? (bool?)false : null,
                null,
                null,
                null,
                null,
                null);
    }

    private sealed class FakeDeviceKeyStore : IDeviceKeyStore
    {
        public Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken)
            => Task.FromResult("-----BEGIN PUBLIC KEY-----\npreview\n-----END PUBLIC KEY-----");
    }

    private sealed class FakeBootstrapWriter : ISetupWizardBootstrapWriter
    {
        public void Write(SetupWizardBootstrapConfig config)
        {
            // preview must not touch machine environment
        }
    }

    private sealed class FakeCompletionAction : ISetupWizardCompletionAction
    {
        public void Complete()
        {
            // preview must not start the Agent service
        }
    }
}
#endif
