#if DEBUG
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;
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

    private sealed class FakeApiClient : ISetupWizardApiClient
    {
        public Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken)
            => Task.FromResult(new InstallDiscoverResponse("Preview Owner", [BuildBranch()]));

        public Task<InstallCreateSeatResponse> CreateSeatAsync(
            string ownerCode,
            Guid branchId,
            Guid zoneId,
            string name,
            CancellationToken cancellationToken)
            => Task.FromResult(new InstallCreateSeatResponse(
                OrgId, branchId, zoneId, Guid.NewGuid(), name, SortOrder: 99));

        public Task<InstallEnrollResponse> EnrollAsync(InstallEnrollRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new InstallEnrollResponse(
                OrgId,
                request.BranchId,
                DeviceId: Guid.NewGuid(),
                CredentialId: Guid.NewGuid(),
                CredentialSecret: "preview-secret",
                EnrollmentState: DeviceEnrollmentStateNames.Pending,
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
                .Select(i => MakeSeat($"PC-{i:D2}", i, MainHallZoneId, "Main Hall", isOccupied: i % 3 == 0))
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
                    new FloorMapZoneDto(MainHallZoneId, "Main Hall", 1),
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
