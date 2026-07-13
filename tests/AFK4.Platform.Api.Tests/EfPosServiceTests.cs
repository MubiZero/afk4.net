using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class EfPosServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T15:00:00Z");

    [Fact]
    public async Task CreateSaleAsync_RequiresOpenShift()
    {
        await using var db = CreateDbContext();
        var product = await SeedProductAsync(db);
        var service = CreateService(db);
        var request = CreateSaleRequest(Guid.NewGuid(), product.ProductId, "sale-001");

        var result = await service.CreateSaleAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Contains("open shift", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.PosSales.ToListAsync());
    }

    [Fact]
    public async Task CreateSaleAsync_SnapshotsProductNameQuantityUnitPriceLineTotalAndSaleTotal()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, name: "Cola 0.5", priceMinorUnits: 1200);
        var service = CreateService(db);

        var result = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(shift.ShiftId, product.ProductId, "sale-001", quantity: 2),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(PosSaleStateNames.Draft, result.Response.State);
        Assert.Equal(new MoneyDto("TJS", 2400), result.Response.Total);
        var line = Assert.Single(result.Response.Lines);
        Assert.Equal("Cola 0.5", line.ProductName);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(new MoneyDto("TJS", 1200), line.UnitPrice);
        Assert.Equal(new MoneyDto("TJS", 2400), line.LineTotal);

        var sale = await db.PosSales.SingleAsync();
        var persistedLine = await db.PosSaleLines.SingleAsync();
        Assert.Equal(result.Response.PosSaleId, sale.PosSaleId);
        Assert.Equal(shift.ShiftId, sale.ShiftId);
        Assert.Equal(2400, sale.TotalMinorUnits);
        Assert.Equal("Cola 0.5", persistedLine.ProductName);
        Assert.Equal(1200, persistedLine.UnitPriceMinorUnits);
        Assert.Equal(2400, persistedLine.LineTotalMinorUnits);
    }

    [Fact]
    public async Task CreateSaleAsync_PersistsOptionalPlayerAccount()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        var player = await SeedPlayerAsync(db);
        var service = CreateService(db);

        var result = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(shift.ShiftId, product.ProductId, "sale-player-001", playerAccountId: player.PlayerAccountId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(player.PlayerAccountId, result.Response.PlayerAccountId);

        var sale = await db.PosSales.SingleAsync();
        Assert.Equal(player.PlayerAccountId, sale.PlayerAccountId);
    }

    [Fact]
    public async Task CreateSaleAsync_RejectsUnknownPlayerAccount()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        var service = CreateService(db);

        var result = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(
                shift.ShiftId,
                product.ProductId,
                "sale-player-missing-001",
                playerAccountId: Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.NotFound);
        Assert.Empty(await db.PosSales.ToListAsync());
    }

    [Fact]
    public async Task CreateSaleAsync_RejectsMissingProductAndQuantityLessThanOne()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        var service = CreateService(db);

        var missing = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(shift.ShiftId, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), "sale-missing-001"),
            CancellationToken.None);
        var badQuantity = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(shift.ShiftId, product.ProductId, "sale-quantity-001", quantity: 0),
            CancellationToken.None);

        Assert.True(missing.NotFound);
        Assert.False(badQuantity.Succeeded);
        Assert.False(badQuantity.Conflict);
        Assert.Empty(await db.PosSales.ToListAsync());
    }

    [Fact]
    public async Task PaySaleAsync_ManualCashMovesDraftToPaidAndCreatesPaymentReceiptAndStockMovement()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);

        var result = await service.PaySaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            ManualPaymentRequest("pay-001", amountMinorUnits: 2400),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(PosSaleStateNames.Paid, result.Response.State);
        Assert.Equal(Now, result.Response.PaidAtUtc);
        Assert.NotNull(result.Response.LatestReceipt);
        Assert.Equal("POS-20260513-0001", result.Response.LatestReceipt.ReceiptNumber);
        Assert.Equal("sale", result.Response.LatestReceipt.ReceiptType);

        var payment = await db.Payments.SingleAsync();
        Assert.Equal(sale.PosSaleId, payment.PosSaleId);
        Assert.Equal(PaymentMethodNames.Cash, payment.PaymentMethod);
        Assert.Equal("payment", payment.PaymentKind);
        Assert.Equal(2400, payment.AmountMinorUnits);

        var receipt = await db.Receipts.SingleAsync();
        Assert.Equal("POS-20260513-0001", receipt.ReceiptNumber);
        Assert.Equal("sale", receipt.ReceiptType);
        Assert.Equal(2400, receipt.TotalMinorUnits);
        Assert.Equal("ru", receipt.Locale);

        var movement = await db.StockMovements.SingleAsync(movement => movement.MovementType == StockMovementTypeNames.Sale);
        Assert.Equal(-2, movement.QuantityDelta);
        Assert.Equal(product.ProductId, movement.ProductId);
    }

    [Fact]
    public async Task PaySaleAsync_UsesAverageCostForTrackedLineAndZeroForService()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var tracked = await SeedProductAsync(db, avgCostMinorUnits: 275);
        var serviceProduct = await SeedProductAsync(db, trackStock: false, avgCostMinorUnits: 999);
        await SeedStockAsync(db, tracked.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var created = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreatePosSaleRequest(
                TestIds.OrganizationId,
                shift.ShiftId,
                [
                    new PosSaleLineDto(tracked.ProductId, "", 1, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0)),
                    new PosSaleLineDto(serviceProduct.ProductId, "", 1, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))
                ],
                "sale-cost-001"),
            CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.NotNull(created.Response);

        var paid = await service.PaySaleAsync(
            created.Response.PosSaleId,
            ActorStaffUserId,
            ManualPaymentRequest("pay-cost-001", amountMinorUnits: 2400),
            CancellationToken.None);
        Assert.True(paid.Succeeded);

        var persistedLines = await db.PosSaleLines.ToListAsync();
        var trackedLine = persistedLines.Single(line => line.ProductId == tracked.ProductId);
        var serviceLine = persistedLines.Single(line => line.ProductId == serviceProduct.ProductId);
        var movements = await db.StockMovements.ToListAsync();
        var saleMovement = movements.Single(movement =>
            movement.ProductId == tracked.ProductId && movement.MovementType == StockMovementTypeNames.Sale);

        Assert.Equal(275, trackedLine.UnitCostMinorUnits);
        Assert.Equal(275, saleMovement.UnitCostMinorUnits);
        Assert.Equal(0, serviceLine.UnitCostMinorUnits);
        Assert.DoesNotContain(movements, movement => movement.ProductId == serviceProduct.ProductId);
    }

    [Fact]
    public async Task PaySaleAsync_StampsBranchPreferredLocaleOnReceipt()
    {
        await using var db = CreateDbContext();
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Slug = "main",
            Name = "Main",
            City = "Dushanbe",
            PreferredLocale = "tg",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);

        await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId, ManualPaymentRequest("pay-loc", 2400), CancellationToken.None);

        var receipt = await db.Receipts.SingleAsync();
        Assert.Equal("tg", receipt.Locale);
    }

    [Fact]
    public async Task PaySaleAsync_StockReachesReorderThreshold_EnqueuesLowStockAlert()
    {
        await using var db = CreateDbContext();
        await SeedOwnerAsync(db);
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        product.ReorderThreshold = 3;
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        await db.SaveChangesAsync();
        var recorder = new RecordingNotificationService();
        var notifier = new EfLowStockNotifier(new EfOrganizationOwnerResolver(db), recorder, db);
        var service = CreateService(db, notifier);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId); // quantity 2 → stock 3 == threshold

        var result = await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId,
            ManualPaymentRequest("pay-001", amountMinorUnits: 2400), CancellationToken.None);

        Assert.True(result.Succeeded);
        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.LowStock, request.TemplateKey);
        Assert.Equal("3", request.Tokens["stockOnHand"]);
    }

    [Fact]
    public async Task PaySaleAsync_RejectsInsufficientStockForTrackedProducts()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 1);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);

        var result = await service.PaySaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            ManualPaymentRequest("pay-001", amountMinorUnits: 2400),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.Receipts.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(movement => movement.MovementType == StockMovementTypeNames.Sale).ToListAsync());
        var unchanged = await db.PosSales.SingleAsync();
        Assert.Equal(PosSaleStateNames.Draft, unchanged.State);
    }

    [Fact]
    public async Task PaySaleAsync_ReplaysSameIdempotencyKeyWithoutDuplicateSideEffects()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);
        var request = ManualPaymentRequest("pay-001", amountMinorUnits: 2400);

        var first = await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.PosSaleId, second.Response.PosSaleId);
        Assert.Single(await db.Payments.ToListAsync());
        Assert.Single(await db.Receipts.ToListAsync());
        Assert.Single(await db.StockMovements.Where(movement => movement.MovementType == StockMovementTypeNames.Sale).ToListAsync());
    }

    [Fact]
    public async Task RefundSaleAsync_MovesPaidToRefundedAndWritesPositiveStockMovementAndRefundReceipt()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);
        await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId, ManualPaymentRequest("pay-001", 2400), CancellationToken.None);

        var result = await service.RefundSaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            new RefundPosSaleRequest(TestIds.OrganizationId, "customer returned unopened item", "refund-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(PosSaleStateNames.Refunded, result.Response.State);
        Assert.Equal(Now, result.Response.RefundedAtUtc);
        Assert.NotNull(result.Response.LatestReceipt);
        Assert.Equal("REF-20260513-0001", result.Response.LatestReceipt.ReceiptNumber);
        Assert.Equal("refund", result.Response.LatestReceipt.ReceiptType);

        var refundReceipt = await db.Receipts.SingleAsync(receipt => receipt.ReceiptType == "refund");
        Assert.Equal("REF-20260513-0001", refundReceipt.ReceiptNumber);
        Assert.Equal(2400, refundReceipt.TotalMinorUnits);

        var refundMovement = await db.StockMovements.SingleAsync(movement => movement.MovementType == StockMovementTypeNames.Refund);
        Assert.Equal(2, refundMovement.QuantityDelta);
        Assert.Equal(product.ProductId, refundMovement.ProductId);
    }

    [Fact]
    public async Task RefundSaleAsync_ReusesOriginalCostAndReconcilesCurrentAverage()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, avgCostMinorUnits: 400);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 10);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);
        await service.PaySaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            ManualPaymentRequest("pay-refund-cost-001", 2400),
            CancellationToken.None);

        var inventoryService = new EfInventoryService(db, new FixedTimeProvider(Now));
        await inventoryService.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateStockMovementRequest(
                TestIds.OrganizationId,
                product.ProductId,
                StockMovementTypeNames.Purchase,
                30,
                new MoneyDto("TJS", 600),
                "restock",
                "purchase-refund-cost-001"),
            CancellationToken.None);

        await service.RefundSaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            new RefundPosSaleRequest(TestIds.OrganizationId, "return", "refund-cost-001"),
            CancellationToken.None);

        var refundMovement = await db.StockMovements.SingleAsync(movement =>
            movement.ProductId == product.ProductId && movement.MovementType == StockMovementTypeNames.Refund);
        product = await db.PosProducts.SingleAsync(candidate => candidate.ProductId == product.ProductId);

        Assert.Equal(400, refundMovement.UnitCostMinorUnits);
        Assert.Equal(550, product.AvgCostMinorUnits);
    }

    [Fact]
    public async Task VoidSaleAsync_MovesDraftToVoidedAndWritesNoStockMovement()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);

        var result = await service.VoidSaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            new VoidPosSaleRequest(TestIds.OrganizationId, "mistaken draft", "void-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(PosSaleStateNames.Voided, result.Response.State);
        Assert.Equal(Now, result.Response.VoidedAtUtc);
        Assert.Empty(await db.StockMovements.ToListAsync());
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.Receipts.ToListAsync());
    }

    [Fact]
    public async Task VoidSaleAsync_RejectsPaidSale()
    {
        await using var db = CreateDbContext();
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db);
        await SeedStockAsync(db, product.ProductId, quantityDelta: 5);
        var service = CreateService(db);
        var sale = await CreateSaleAsync(service, shift.ShiftId, product.ProductId);
        await service.PaySaleAsync(sale.PosSaleId, ActorStaffUserId, ManualPaymentRequest("pay-001", 2400), CancellationToken.None);

        var result = await service.VoidSaleAsync(
            sale.PosSaleId,
            ActorStaffUserId,
            new VoidPosSaleRequest(TestIds.OrganizationId, "mistaken draft", "void-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        var paid = await db.PosSales.SingleAsync();
        Assert.Equal(PosSaleStateNames.Paid, paid.State);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfPosService CreateService(PlatformDbContext db, ILowStockNotifier? lowStockNotifier = null)
    {
        return new EfPosService(
            db,
            new ManualPaymentProvider(),
            new ReceiptNumberGenerator(db),
            new FixedTimeProvider(Now),
            new EfInventoryCostService(db),
            lowStockNotifier);
    }

    private static async Task SeedOwnerAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        var ownerId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = TestIds.OrganizationId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = "owner@club.example", PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, RoleName = StaffRoleNames.Owner
        });
        db.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Name = "Central", CreatedAtUtc = Now });
        await db.SaveChangesAsync();
    }

    private static async Task<ShiftEntity> SeedOpenShiftAsync(PlatformDbContext db)
    {
        var shift = new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "front register",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        };

        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        return shift;
    }

    private static async Task<PosProductEntity> SeedProductAsync(
        PlatformDbContext db,
        string name = "Cola 0.5",
        long priceMinorUnits = 1200,
        bool trackStock = true,
        bool allowNegativeStock = false,
        long avgCostMinorUnits = 0)
    {
        var category = new PosProductCategoryEntity
        {
            CategoryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "DRINKS",
            IsActive = true,
            CreatedAtUtc = Now
        };
        var product = new PosProductEntity
        {
            ProductId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            CategoryId = category.CategoryId,
            Name = name,
            Sku = $"SKU-{Guid.NewGuid():N}",
            CurrencyCode = "TJS",
            PriceMinorUnits = priceMinorUnits,
            AvgCostMinorUnits = avgCostMinorUnits,
            TrackStock = trackStock,
            AllowNegativeStock = allowNegativeStock,
            IsActive = true,
            CreatedAtUtc = Now
        };

        db.PosProductCategories.Add(category);
        db.PosProducts.Add(product);
        await db.SaveChangesAsync();

        return product;
    }

    private static async Task SeedStockAsync(PlatformDbContext db, Guid productId, int quantityDelta)
    {
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ProductId = productId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = quantityDelta,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 900,
            Reason = "test stock",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PlayerAccountEntity> SeedPlayerAsync(PlatformDbContext db)
    {
        var player = new PlayerAccountEntity
        {
            PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        };

        db.PlayerAccounts.Add(player);
        await db.SaveChangesAsync();

        return player;
    }

    private static async Task<PosSaleDto> CreateSaleAsync(EfPosService service, Guid shiftId, Guid productId)
    {
        var result = await service.CreateSaleAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            CreateSaleRequest(shiftId, productId, $"sale-{Guid.NewGuid():N}", quantity: 2),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private static CreatePosSaleRequest CreateSaleRequest(
        Guid shiftId,
        Guid productId,
        string idempotencyKey,
        int quantity = 1,
        Guid? playerAccountId = null)
    {
        return new CreatePosSaleRequest(
            TestIds.OrganizationId,
            shiftId,
            [new PosSaleLineDto(productId, "", quantity, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))],
            idempotencyKey,
            playerAccountId);
    }

    private static ManualPaymentRequest ManualPaymentRequest(string idempotencyKey, long amountMinorUnits)
    {
        return new ManualPaymentRequest(
            TestIds.OrganizationId,
            PaymentMethodNames.Cash,
            new MoneyDto("TJS", amountMinorUnits),
            "cash drawer",
            idempotencyKey);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
