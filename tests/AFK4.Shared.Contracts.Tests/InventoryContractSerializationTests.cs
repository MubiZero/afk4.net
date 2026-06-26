using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;

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
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            CreatedByDisplayName: "Олег С.");

        var copy = JsonSerializer.Deserialize<StockMovementDto>(
            JsonSerializer.Serialize(movement, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(StockMovementTypeNames.Adjustment, copy.MovementType);
        Assert.Equal(-2, copy.QuantityDelta);
        Assert.Equal("Олег С.", copy!.CreatedByDisplayName);
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

    [Fact]
    public void PosProductDto_RoundTrips_AvgCostMinorUnits()
    {
        var dto = new PosProductDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Snickers", "SNICKERS", new MoneyDto("TJS", 1000),
            true, false, true, 15, DateTimeOffset.UnixEpoch,
            ReorderThreshold: 10, AvailableInShell: false, AvgCostMinorUnits: 500);
        var json = JsonSerializer.Serialize(dto);
        var back = JsonSerializer.Deserialize<PosProductDto>(json);
        Assert.Equal(500, back!.AvgCostMinorUnits);
    }

    [Fact]
    public void ProductBarcodeDto_RoundTrips()
    {
        var dto = new ProductBarcodeDto(Guid.NewGuid(), Guid.NewGuid(), "4601234567890", IsPrimary: true);
        var back = JsonSerializer.Deserialize<ProductBarcodeDto>(JsonSerializer.Serialize(dto));
        Assert.Equal("4601234567890", back!.Code);
        Assert.True(back.IsPrimary);
    }

    [Fact]
    public void PosProductDto_RoundTrips_Barcodes()
    {
        var dto = new PosProductDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Snickers", "SNICKERS", new MoneyDto("TJS", 1000),
            true, false, true, 15, DateTimeOffset.UnixEpoch,
            ReorderThreshold: 10, AvailableInShell: false, AvgCostMinorUnits: 500,
            Barcodes: new[] { "4601234567890", "0000111122223" });
        var back = JsonSerializer.Deserialize<PosProductDto>(JsonSerializer.Serialize(dto));
        Assert.Equal(2, back!.Barcodes.Count);
        Assert.Equal("4601234567890", back.Barcodes[0]);
    }

    [Fact]
    public void PosProductDto_Barcodes_DefaultsToEmpty()
    {
        var dto = new PosProductDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Cola", "COLA", new MoneyDto("TJS", 500),
            true, false, true, 3, DateTimeOffset.UnixEpoch);
        Assert.NotNull(dto.Barcodes);
        Assert.Empty(dto.Barcodes);
    }
}
