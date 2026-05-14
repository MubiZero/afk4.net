using System.Text.Json;
using AFK4.Shared.Contracts.Audit;

namespace AFK4.Shared.Contracts.Tests;

public sealed class AuditContractSerializationTests
{
    [Fact]
    public void AuditSearchResultDto_RoundTripsThroughJson()
    {
        var record = new AuditRecordDto(
            AuditRecordId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ActorStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            Action: "billing.refund",
            TargetType: "LedgerEntry",
            TargetId: "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb",
            Outcome: "Succeeded",
            SourceApp: "PlatformApi",
            DetailsJson: """{"reason":"mistake"}""",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"));
        var result = new AuditSearchResultDto([record], Limit: 50);

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<AuditSearchResultDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(50, copy.Limit);
        var readBack = Assert.Single(copy.Records);
        Assert.Equal(record.AuditRecordId, readBack.AuditRecordId);
        Assert.Equal("billing.refund", readBack.Action);
        Assert.Equal("""{"reason":"mistake"}""", readBack.DetailsJson);
    }
}
