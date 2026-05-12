using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class AuditRecordWriterTests
{
    [Fact]
    public async Task WriteAsync_AppendsAuditRecordWithoutMutatingExistingRows()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var now = DateTimeOffset.Parse("2026-05-12T00:00:00Z");

        await using var dbContext = new PlatformDbContext(options);
        var writer = new AuditRecordWriter(dbContext, TimeProvider.System);

        await writer.WriteAsync(new AuditRecordWriteRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ActorStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            Action: AuditActionNames.CreateDeviceEnrollmentCode,
            TargetType: "DeviceEnrollmentCode",
            TargetId: "AFK4-TEST-CODE",
            Outcome: AuditOutcome.Succeeded,
            SourceApp: "PlatformApi",
            DetailsJson: """{"expiresInSeconds":300}"""),
            CancellationToken.None);

        var record = await dbContext.AuditRecords.SingleAsync();

        Assert.Equal(AuditActionNames.CreateDeviceEnrollmentCode, record.Action);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal("AFK4-TEST-CODE", record.TargetId);
        Assert.Equal("""{"expiresInSeconds":300}""", record.DetailsJson);
        Assert.True(record.CreatedAtUtc > now);
    }
}
