using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class InstalledAppReportContractSerializationTests
{
    [Fact]
    public void InstalledAppReportRequest_RoundTripsThroughJson()
    {
        var request = new InstalledAppReportRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            ReportedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Apps:
            [
                new InstalledAppDto(
                    DisplayName: "Counter-Strike 2",
                    Version: "2.0.0",
                    Publisher: "Valve",
                    InstallLocation: @"C:\Games\Counter-Strike 2",
                    InstalledAtUtc: DateTimeOffset.Parse("2026-05-01T08:30:00Z"))
            ]);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<InstalledAppReportRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal(request.BranchId, copy.BranchId);
        Assert.Equal(request.DeviceId, copy.DeviceId);
        Assert.Equal(request.ReportedAtUtc, copy.ReportedAtUtc);
        var app = Assert.Single(copy.Apps);
        Assert.Equal("Counter-Strike 2", app.DisplayName);
        Assert.Equal("2.0.0", app.Version);
        Assert.Equal("Valve", app.Publisher);
        Assert.Equal(@"C:\Games\Counter-Strike 2", app.InstallLocation);
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T08:30:00Z"), app.InstalledAtUtc);
    }
}
