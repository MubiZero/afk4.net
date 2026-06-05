#if DEBUG
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Preview;

/// <summary>
/// Provides fake services so the wizard can be launched end-to-end without elevation,
/// network calls, disk writes, or touching the Agent service.
/// Launch with: AFK4.SetupWizard.exe --preview
/// </summary>
internal static class PreviewSetupWizard
{
    public static ISetupWizardApiClient CreateApiClient() => new FakeApiClient();

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
        private static readonly Guid ZoneId = new("00000000-0000-0000-0000-0000000000cc");

        private static InstallBranchDto BuildBranch()
        {
            var freeSeat = Guid.NewGuid();
            var takenSeat = Guid.NewGuid();
            var floorMap = new FloorMapDto(
                BranchId,
                "Preview Club",
                [
                    new SeatStatusDto(freeSeat, "PC-01", ZoneId, "Main Hall", 1, "Free",
                        null, null, null, null, null, null, null, null, null),
                    new SeatStatusDto(takenSeat, "PC-02", ZoneId, "Main Hall", 2, "Occupied",
                        Guid.NewGuid(), "PC-02", true, false, null, null, null, null, null),
                ])
            {
                Zones = [new FloorMapZoneDto(ZoneId, "Main Hall", 1)],
            };
            return new InstallBranchDto(BranchId, "preview-club", "Preview Club", floorMap, [freeSeat]);
        }
    }
}
#endif
