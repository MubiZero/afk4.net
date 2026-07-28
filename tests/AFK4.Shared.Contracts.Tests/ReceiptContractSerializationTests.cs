using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ReceiptContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ReceiptDto_RoundTrips()
    {
        var receiptId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        var saleId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        var createdAtUtc = DateTimeOffset.Parse("2026-05-13T10:01:00Z");
        var receipt = new ReceiptDto(
            receiptId,
            organizationId,
            branchId,
            saleId,
            "POS-20260513-0001",
            "sale",
            new MoneyDto("TJS", 2400),
            createdAtUtc);

        var copy = JsonSerializer.Deserialize<ReceiptDto>(
            JsonSerializer.Serialize(receipt, Options),
            Options);

        Assert.Equal(receipt, copy);
    }

    [Fact]
    public void OrganizationPermissionNames_ExposeReceiptPermissions()
    {
        Assert.Equal("organization.receipts.view", OrganizationPermissionNames.ViewReceipt);
    }
}
