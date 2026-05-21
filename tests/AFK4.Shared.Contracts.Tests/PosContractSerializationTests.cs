using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PosContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateProductCategoryRequest_RoundTrips()
    {
        var request = new CreateProductCategoryRequest(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Drinks",
            "category-001");

        var copy = JsonSerializer.Deserialize<CreateProductCategoryRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void CreateProductRequest_RoundTrips()
    {
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var categoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var request = new CreateProductRequest(
            organizationId,
            categoryId,
            "Cola 0.5",
            "COLA-05",
            new MoneyDto("TJS", 1200),
            trackStock: true,
            allowNegativeStock: false,
            "product-001");

        var copy = JsonSerializer.Deserialize<CreateProductRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void CreatePosSaleRequest_RoundTrips()
    {
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var shiftId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var productId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
        var sale = new CreatePosSaleRequest(
            organizationId,
            shiftId,
            [new PosSaleLineDto(productId, "Cola 0.5", 2, new MoneyDto("TJS", 1200), new MoneyDto("TJS", 2400))],
            "sale-001");

        var copy = JsonSerializer.Deserialize<CreatePosSaleRequest>(
            JsonSerializer.Serialize(sale, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(sale.OrganizationId, copy.OrganizationId);
        Assert.Single(copy.Lines);
        Assert.Equal(2400, copy.Lines[0].LineTotal.MinorUnits);
    }

    [Fact]
    public void PosProductCategoryDto_RoundTrips()
    {
        var category = new PosProductCategoryDto(
            CategoryId: Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Name: "Drinks",
            IsActive: true,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));

        var copy = JsonSerializer.Deserialize<PosProductCategoryDto>(
            JsonSerializer.Serialize(category, Options),
            Options);

        Assert.Equal(category, copy);
    }

    [Fact]
    public void PosProductDto_RoundTrips()
    {
        var product = new PosProductDto(
            ProductId: Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            CategoryId: Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
            Name: "Cola 0.5",
            Sku: "COLA-05",
            Price: new MoneyDto("TJS", 1200),
            TrackStock: true,
            AllowNegativeStock: false,
            IsActive: true,
            StockOnHand: 22,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));

        var copy = JsonSerializer.Deserialize<PosProductDto>(
            JsonSerializer.Serialize(product, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal("COLA-05", copy.Sku);
        Assert.Equal(22, copy.StockOnHand);
    }

    [Fact]
    public void PosSaleDto_RoundTrips()
    {
        var sale = new PosSaleDto(
            PosSaleId: Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            State: PosSaleStateNames.Paid,
            Lines: [new PosSaleLineDto(Guid.Parse("dddddddd-0000-0000-0000-000000000001"), "Cola 0.5", 2, new MoneyDto("TJS", 1200), new MoneyDto("TJS", 2400))],
            Total: new MoneyDto("TJS", 2400),
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            PaidAtUtc: DateTimeOffset.Parse("2026-05-13T10:01:00Z"),
            RefundedAtUtc: null,
            VoidedAtUtc: null,
            LatestReceipt: new ReceiptDto(
                Guid.Parse("ffffffff-0000-0000-0000-000000000001"),
                Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
                "POS-20260513-0001",
                "sale",
                new MoneyDto("TJS", 2400),
                DateTimeOffset.Parse("2026-05-13T10:01:00Z")));

        var copy = JsonSerializer.Deserialize<PosSaleDto>(
            JsonSerializer.Serialize(sale, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(PosSaleStateNames.Paid, copy.State);
        Assert.Single(copy.Lines);
        Assert.Equal(2400, copy.Total.MinorUnits);
        Assert.Equal("POS-20260513-0001", copy.LatestReceipt?.ReceiptNumber);
    }

    [Fact]
    public void RefundAndVoidRequests_RoundTrip()
    {
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var refund = new RefundPosSaleRequest(organizationId, "customer returned unopened item", "refund-001");
        var refundCopy = JsonSerializer.Deserialize<RefundPosSaleRequest>(
            JsonSerializer.Serialize(refund, Options),
            Options);
        var voidRequest = new VoidPosSaleRequest(organizationId, "mistaken draft", "void-001");
        var voidCopy = JsonSerializer.Deserialize<VoidPosSaleRequest>(
            JsonSerializer.Serialize(voidRequest, Options),
            Options);

        Assert.Equal(refund, refundCopy);
        Assert.Equal(voidRequest, voidCopy);
    }

    [Fact]
    public void Constants_ExposeStablePosStateNames()
    {
        Assert.Equal("draft", PosSaleStateNames.Draft);
        Assert.Equal("pending_payment", PosSaleStateNames.PendingPayment);
        Assert.Equal("paid", PosSaleStateNames.Paid);
        Assert.Equal("refunded", PosSaleStateNames.Refunded);
        Assert.Equal("voided", PosSaleStateNames.Voided);
    }

    [Fact]
    public void StaffPermissionNames_ExposePosPermissions()
    {
        Assert.Equal("pos.catalog.manage", StaffPermissionNames.ManagePosCatalog);
        Assert.Equal("pos.sales.create", StaffPermissionNames.CreatePosSale);
        Assert.Equal("pos.sales.pay", StaffPermissionNames.PayPosSale);
        Assert.Equal("pos.sales.refund", StaffPermissionNames.RefundPosSale);
        Assert.Equal("pos.sales.void", StaffPermissionNames.VoidPosSale);
    }
}
