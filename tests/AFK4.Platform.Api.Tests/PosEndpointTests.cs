using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class PosEndpointTests
{
    private static readonly Guid UnknownShiftId = Guid.Parse("11111111-aaaa-4111-8111-111111111111");
    private static readonly Guid UnknownSaleId = Guid.Parse("22222222-aaaa-4222-8222-222222222222");
    private static readonly Guid UnknownReceiptId = Guid.Parse("33333333-aaaa-4333-8333-333333333333");

    [Fact]
    public async Task Phase6Endpoints_WithoutBearer_ReturnUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        foreach (var endpoint in CreateEndpointCases(UnknownShiftId, UnknownSaleId, UnknownReceiptId))
        {
            using var response = await SendAsync(client, endpoint);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Phase6Endpoints_WithUnauthorizedRole_ReturnForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        foreach (var endpoint in CreateEndpointCases(UnknownShiftId, UnknownSaleId, UnknownReceiptId)
                     .Where(endpoint =>
                         endpoint.Path != $"/api/branches/{TestIds.BranchId:D}/pos/catalog" &&
                         endpoint.Path != $"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements"))
        {
            using var response = await SendAsync(client, endpoint);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task InventoryReads_WithCashierWithoutInventoryView_ReturnForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var catalogResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/pos/catalog");
        var stockHistoryResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements");

        Assert.Equal(HttpStatusCode.Forbidden, catalogResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stockHistoryResponse.StatusCode);
    }

    [Fact]
    public async Task Phase6Endpoints_WithBranchManager_RunPosWorkflowAndWriteAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var shift = await PostOkAsync<ShiftDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/shifts/open",
            new OpenShiftRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 50000),
                "front register",
                "shift-open-001"));
        Assert.Equal(ShiftStateNames.Open, shift.State);

        var currentShift = await client.GetFromJsonAsync<ShiftDto>(
            $"/api/branches/{TestIds.BranchId:D}/shifts/current");
        Assert.NotNull(currentShift);
        Assert.Equal(shift.ShiftId, currentShift.ShiftId);

        var cashMovement = await PostOkAsync<CashMovementDto>(
            client,
            $"/api/shifts/{shift.ShiftId:D}/cash-movements",
            new RecordCashMovementRequest(
                TestIds.OrganizationId,
                CashMovementTypeNames.CashIn,
                new MoneyDto("TJS", 5000),
                "cash drawer correction",
                "cash-in-001"));
        Assert.Equal(CashMovementTypeNames.CashIn, cashMovement.MovementType);

        var category = await PostOkAsync<PosProductCategoryDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/pos/categories",
            new CreateProductCategoryRequest(TestIds.OrganizationId, "Drinks", "category-001"));

        var product = await PostOkAsync<PosProductDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/pos/products",
            new CreateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                "Cola 0.5",
                "COLA-05",
                new MoneyDto("TJS", 1200),
                trackStock: true,
                allowNegativeStock: false,
                "product-001"));
        Assert.Equal("COLA-05", product.Sku);

        var stock = await PostOkAsync<StockMovementDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements",
            new CreateStockMovementRequest(
                TestIds.OrganizationId,
                product.ProductId,
                StockMovementTypeNames.Purchase,
                24,
                new MoneyDto("TJS", 900),
                "initial stock",
                "stock-001"));
        Assert.Equal(24, stock.QuantityDelta);

        var stockHistory = await client.GetFromJsonAsync<IReadOnlyList<StockMovementDto>>(
            $"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements?productId={product.ProductId:D}&limit=10");
        Assert.NotNull(stockHistory);
        var stockHistoryRow = Assert.Single(stockHistory);
        Assert.Equal(stock.StockMovementId, stockHistoryRow.StockMovementId);
        Assert.Equal(24, stockHistoryRow.QuantityDelta);

        var updatedProduct = await PatchOkAsync<PosProductDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/pos/products/{product.ProductId:D}",
            new UpdateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                "Cola Zero 0.5",
                "COLA-ZERO-05",
                new MoneyDto("TJS", 1300),
                TrackStock: true,
                AllowNegativeStock: true,
                IsActive: true));
        Assert.Equal("COLA-ZERO-05", updatedProduct.Sku);
        Assert.Equal(24, updatedProduct.StockOnHand);

        var player = await PostOkAsync<PlayerAccountDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/players",
            new CreatePlayerAccountRequest(
                TestIds.OrganizationId,
                "Player One",
                "+992000000001",
                "player-create-001"));

        var catalog = await client.GetFromJsonAsync<IReadOnlyList<PosProductDto>>(
            $"/api/branches/{TestIds.BranchId:D}/pos/catalog");
        Assert.NotNull(catalog);
        Assert.Single(catalog);
        Assert.Equal("COLA-ZERO-05", catalog[0].Sku);
        Assert.Equal(24, catalog[0].StockOnHand);

        var sale = await PostOkAsync<PosSaleDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/pos/sales",
            new CreatePosSaleRequest(
                TestIds.OrganizationId,
                shift.ShiftId,
                [new PosSaleLineDto(product.ProductId, string.Empty, 2, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))],
                "sale-001",
                player.PlayerAccountId));
        Assert.Equal(PosSaleStateNames.Draft, sale.State);
        Assert.Equal(player.PlayerAccountId, sale.PlayerAccountId);

        var paid = await PostOkAsync<PosSaleDto>(
            client,
            $"/api/pos/sales/{sale.PosSaleId:D}/payments/manual",
            new ManualPaymentRequest(
                TestIds.OrganizationId,
                PaymentMethodNames.Cash,
                new MoneyDto("TJS", 2600),
                "cash drawer",
                "pay-001"));
        Assert.Equal(PosSaleStateNames.Paid, paid.State);
        Assert.NotNull(paid.LatestReceipt);
        Assert.StartsWith("POS-", paid.LatestReceipt.ReceiptNumber, StringComparison.Ordinal);

        var readSale = await client.GetFromJsonAsync<PosSaleDto>($"/api/pos/sales/{sale.PosSaleId:D}");
        Assert.NotNull(readSale);
        Assert.Equal(PosSaleStateNames.Paid, readSale.State);
        Assert.Equal(player.PlayerAccountId, readSale.PlayerAccountId);
        Assert.NotNull(readSale.LatestReceipt);
        Assert.Equal(paid.LatestReceipt.ReceiptNumber, readSale.LatestReceipt.ReceiptNumber);

        var receiptId = await LoadReceiptIdAsync(factory, sale.PosSaleId, "sale");
        var receipt = await client.GetFromJsonAsync<ReceiptDto>($"/api/receipts/{receiptId:D}");
        Assert.NotNull(receipt);
        Assert.StartsWith("POS-", receipt.ReceiptNumber, StringComparison.Ordinal);
        Assert.EndsWith("-0001", receipt.ReceiptNumber, StringComparison.Ordinal);

        var refunded = await PostOkAsync<PosSaleDto>(
            client,
            $"/api/pos/sales/{sale.PosSaleId:D}/refunds",
            new RefundPosSaleRequest(
                TestIds.OrganizationId,
                "customer returned unopened item",
                "refund-001"));
        Assert.Equal(PosSaleStateNames.Refunded, refunded.State);
        Assert.NotNull(refunded.LatestReceipt);
        Assert.StartsWith("REF-", refunded.LatestReceipt.ReceiptNumber, StringComparison.Ordinal);

        var draftToVoid = await PostOkAsync<PosSaleDto>(
            client,
            $"/api/branches/{TestIds.BranchId:D}/pos/sales",
            new CreatePosSaleRequest(
                TestIds.OrganizationId,
                shift.ShiftId,
                [new PosSaleLineDto(product.ProductId, string.Empty, 1, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))],
                "sale-void-001"));
        var voided = await PostOkAsync<PosSaleDto>(
            client,
            $"/api/pos/sales/{draftToVoid.PosSaleId:D}/void",
            new VoidPosSaleRequest(TestIds.OrganizationId, "mistaken draft", "void-001"));
        Assert.Equal(PosSaleStateNames.Voided, voided.State);

        var closed = await PostOkAsync<ShiftDto>(
            client,
            $"/api/shifts/{shift.ShiftId:D}/close",
            new CloseShiftRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 55000),
                "end of shift",
                "shift-close-001"));
        Assert.Equal(ShiftStateNames.Closed, closed.State);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditActions = await dbContext.AuditRecords
            .Select(audit => audit.Action)
            .ToListAsync();
        Assert.Contains(AuditActionNames.OpenShift, auditActions);
        Assert.Contains(AuditActionNames.RecordCashMovement, auditActions);
        Assert.Contains(AuditActionNames.CreateProductCategory, auditActions);
        Assert.Contains(AuditActionNames.CreateProduct, auditActions);
        Assert.Contains(AuditActionNames.CreateStockMovement, auditActions);
        Assert.Contains(AuditActionNames.UpdateProduct, auditActions);
        Assert.Contains(AuditActionNames.CreatePlayerAccount, auditActions);
        Assert.Contains(AuditActionNames.CreatePosSale, auditActions);
        Assert.Contains(AuditActionNames.PayPosSale, auditActions);
        Assert.Contains(AuditActionNames.RefundPosSale, auditActions);
        Assert.Contains(AuditActionNames.VoidPosSale, auditActions);
        Assert.Contains(AuditActionNames.CloseShift, auditActions);
    }

    [Fact]
    public async Task GetPosSale_ForCrossBranchSale_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var saleId = await SeedCrossBranchSaleAsync(factory);

        var response = await client.GetAsync($"/api/pos/sales/{saleId:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
    }

    private static IReadOnlyList<EndpointCase> CreateEndpointCases(Guid shiftId, Guid saleId, Guid receiptId)
    {
        return
        [
            new EndpointCase(
                HttpMethod.Post,
                $"/api/branches/{TestIds.BranchId:D}/shifts/open",
                new OpenShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 50000), "front register", "shift-open-001")),
            new EndpointCase(
                HttpMethod.Get,
                $"/api/branches/{TestIds.BranchId:D}/shifts/current",
                null),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/shifts/{shiftId:D}/cash-movements",
                new RecordCashMovementRequest(TestIds.OrganizationId, CashMovementTypeNames.CashIn, new MoneyDto("TJS", 1000), "cash in", "cash-in-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/shifts/{shiftId:D}/close",
                new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 51000), "close", "shift-close-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/branches/{TestIds.BranchId:D}/pos/categories",
                new CreateProductCategoryRequest(TestIds.OrganizationId, "Drinks", "category-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/branches/{TestIds.BranchId:D}/pos/products",
                new CreateProductRequest(TestIds.OrganizationId, Guid.NewGuid(), "Cola 0.5", "COLA-05", new MoneyDto("TJS", 1200), true, false, "product-001")),
            new EndpointCase(
                HttpMethod.Patch,
                $"/api/branches/{TestIds.BranchId:D}/pos/products/{Guid.NewGuid():D}",
                new UpdateProductRequest(TestIds.OrganizationId, Guid.NewGuid(), "Cola 0.5", "COLA-05", new MoneyDto("TJS", 1200), true, false, true)),
            new EndpointCase(
                HttpMethod.Get,
                $"/api/branches/{TestIds.BranchId:D}/pos/catalog",
                null),
            new EndpointCase(
                HttpMethod.Get,
                $"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements",
                null),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/branches/{TestIds.BranchId:D}/inventory/stock-movements",
                new CreateStockMovementRequest(TestIds.OrganizationId, Guid.NewGuid(), StockMovementTypeNames.Purchase, 24, new MoneyDto("TJS", 900), "initial stock", "stock-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/branches/{TestIds.BranchId:D}/pos/sales",
                new CreatePosSaleRequest(TestIds.OrganizationId, shiftId, [new PosSaleLineDto(Guid.NewGuid(), string.Empty, 1, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))], "sale-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/pos/sales/{saleId:D}/payments/manual",
                new ManualPaymentRequest(TestIds.OrganizationId, PaymentMethodNames.Cash, new MoneyDto("TJS", 1200), "cash drawer", "pay-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/pos/sales/{saleId:D}/refunds",
                new RefundPosSaleRequest(TestIds.OrganizationId, "customer return", "refund-001")),
            new EndpointCase(
                HttpMethod.Post,
                $"/api/pos/sales/{saleId:D}/void",
                new VoidPosSaleRequest(TestIds.OrganizationId, "mistaken draft", "void-001")),
            new EndpointCase(
                HttpMethod.Get,
                $"/api/pos/sales/{saleId:D}",
                null),
            new EndpointCase(
                HttpMethod.Get,
                $"/api/receipts/{receiptId:D}",
                null)
        ];
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, EndpointCase endpoint)
    {
        if (endpoint.Method == HttpMethod.Get)
        {
            return await client.GetAsync(endpoint.Path);
        }

        if (endpoint.Method == HttpMethod.Patch)
        {
            return await client.PatchAsJsonAsync(endpoint.Path, endpoint.Body);
        }

        return await client.PostAsJsonAsync(endpoint.Path, endpoint.Body);
    }

    private static async Task<TResponse> PostOkAsync<TResponse>(
        HttpClient client,
        string path,
        object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        var body = await response.Content.ReadFromJsonAsync<TResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body;
    }

    private static async Task<TResponse> PatchOkAsync<TResponse>(
        HttpClient client,
        string path,
        object request)
    {
        using var response = await client.PatchAsJsonAsync(path, request);
        var body = await response.Content.ReadFromJsonAsync<TResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body;
    }

    private static async Task<Guid> LoadReceiptIdAsync(
        PlatformApiFactory factory,
        Guid saleId,
        string receiptType)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await dbContext.Receipts
            .Where(receipt => receipt.PosSaleId == saleId && receipt.ReceiptType == receiptType)
            .Select(receipt => receipt.ReceiptId)
            .SingleAsync();
    }

    private static async Task<Guid> SeedCrossBranchSaleAsync(PlatformApiFactory factory)
    {
        var crossBranchId = Guid.Parse("44444444-aaaa-4444-8444-444444444444");
        var shiftId = Guid.Parse("55555555-aaaa-4555-8555-555555555555");
        var saleId = Guid.Parse("66666666-aaaa-4666-8666-666666666666");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = crossBranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Other Branch",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z")
        });
        dbContext.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = crossBranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            OpeningNote = "cross branch",
            ClosingNote = string.Empty,
            OpenedAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z")
        });
        dbContext.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = crossBranchId,
            ShiftId = shiftId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = PosSaleStateNames.Draft,
            CurrencyCode = "TJS",
            TotalMinorUnits = 1200,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z")
        });
        await dbContext.SaveChangesAsync();

        return saleId;
    }

    private sealed record EndpointCase(
        HttpMethod Method,
        string Path,
        object? Body);
}
