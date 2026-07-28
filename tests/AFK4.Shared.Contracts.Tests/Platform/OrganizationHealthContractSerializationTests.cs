using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Health;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class OrganizationHealthContractSerializationTests
{
    [Fact]
    public void OrganizationHealthDto_RoundTripsRecentErrors()
    {
        var health = new OrganizationHealthDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Status: OrganizationStatusNames.Active,
            BranchCount: 1,
            DeviceCount: 24,
            ActiveStaffUserCount: 6,
            LatestStaffSignInAtUtc: DateTimeOffset.Parse("2026-05-23T07:55:00Z"),
            LatestMigration: "20260523103547_AddSaasControlPlaneFoundation",
            RecentErrorCount: 2,
            RecentErrors: [
                new OrganizationHealthErrorDto(
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T07:50:00Z"),
                    Source: "PlatformApi",
                    Action: "sessions.start",
                    Outcome: "Denied",
                    Message: "Staff user is not assigned to this branch."),
                new OrganizationHealthErrorDto(
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-23T07:51:00Z"),
                    Source: "PlatformApi",
                    Action: "pos.sales.create",
                    Outcome: "Denied",
                    Message: null)
            ]);

        var json = JsonSerializer.Serialize(health);
        var copy = JsonSerializer.Deserialize<OrganizationHealthDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(health.OrganizationId, copy.OrganizationId);
        Assert.Equal(health.Status, copy.Status);
        Assert.Equal(health.BranchCount, copy.BranchCount);
        Assert.Equal(health.DeviceCount, copy.DeviceCount);
        Assert.Equal(health.ActiveStaffUserCount, copy.ActiveStaffUserCount);
        Assert.Equal(health.LatestMigration, copy.LatestMigration);
        Assert.Equal(2, copy.RecentErrors.Count);
        Assert.Equal("sessions.start", copy.RecentErrors[0].Action);
        Assert.Null(copy.RecentErrors[1].Message);
    }
}
