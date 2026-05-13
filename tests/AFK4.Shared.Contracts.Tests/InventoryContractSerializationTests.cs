using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Inventory;

namespace AFK4.Shared.Contracts.Tests;

public sealed class InventoryContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateStockMovementRequest_RoundTrips()
    {
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var productId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
        var request = new CreateStockMovementRequest(
            organizationId,
            productId,
            StockMovementTypeNames.Purchase,
            24,
            new MoneyDto("TJS", 900),
            "initial stock",
            "stock-001");

        var copy = JsonSerializer.Deserialize<CreateStockMovementRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void StockMovementDto_RoundTrips()
    {
        var movement = new StockMovementDto(
            StockMovementId: Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ProductId: Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
            MovementType: StockMovementTypeNames.Adjustment,
            QuantityDelta: -2,
            UnitCost: new MoneyDto("TJS", 900),
            Reason: "damaged stock",
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));

        var copy = JsonSerializer.Deserialize<StockMovementDto>(
            JsonSerializer.Serialize(movement, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(StockMovementTypeNames.Adjustment, copy.MovementType);
        Assert.Equal(-2, copy.QuantityDelta);
    }

    [Fact]
    public void InventoryStockDto_RoundTrips()
    {
        var stock = new InventoryStockDto(
            ProductId: Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
            ProductName: "Cola 0.5",
            Sku: "COLA-05",
            TrackStock: true,
            StockOnHand: 22);

        var copy = JsonSerializer.Deserialize<InventoryStockDto>(
            JsonSerializer.Serialize(stock, Options),
            Options);

        Assert.Equal(stock, copy);
    }

    [Fact]
    public void Constants_ExposeStableInventoryNames()
    {
        Assert.Equal("purchase", StockMovementTypeNames.Purchase);
        Assert.Equal("sale", StockMovementTypeNames.Sale);
        Assert.Equal("refund", StockMovementTypeNames.Refund);
        Assert.Equal("adjustment", StockMovementTypeNames.Adjustment);
    }

    [Fact]
    public void StaffPermissionNames_ExposeInventoryPermissions()
    {
        Assert.Equal("inventory.stock.manage", StaffPermissionNames.ManageInventoryStock);
        Assert.Equal("inventory.view", StaffPermissionNames.ViewInventory);
    }
}
