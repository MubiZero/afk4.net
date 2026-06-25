using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfInventoryServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T14:00:00Z");

    [Fact]
    public async Task CreateCategoryAsync_CreatesUniqueBranchCategoryAndIsIdempotent()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreateProductCategoryRequest(TestIds.OrganizationId, "Drinks", "category-001");

        var first = await service.CreateCategoryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreateCategoryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.CategoryId, second.Response.CategoryId);
        Assert.Equal("DRINKS", first.Response.Name);
        Assert.True(first.Response.IsActive);
        Assert.Single(await db.PosProductCategories.ToListAsync());
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task CreateCategoryAsync_RejectsDuplicateNameInSameBranchButAllowsAnotherBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreateProductCategoryRequest(TestIds.OrganizationId, "Drinks", "category-001");
        var duplicate = request with { Name = " drinks ", IdempotencyKey = "category-002" };

        await service.CreateCategoryAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var sameBranch = await service.CreateCategoryAsync(TestIds.BranchId, ActorStaffUserId, duplicate, CancellationToken.None);
        var otherBranch = await service.CreateCategoryAsync(OtherBranchId, ActorStaffUserId, duplicate, CancellationToken.None);

        Assert.False(sameBranch.Succeeded);
        Assert.False(sameBranch.Conflict);
        Assert.True(otherBranch.Succeeded);
        Assert.Equal(2, await db.PosProductCategories.CountAsync());
    }

    [Fact]
    public async Task CreateProductAsync_CreatesProductWithCategorySkuPriceAndStockFlags()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);
        var request = ProductRequest(category.CategoryId, "product-001");

        var first = await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.ProductId, second.Response.ProductId);
        Assert.Equal(category.CategoryId, first.Response.CategoryId);
        Assert.Equal("Cola 0.5", first.Response.Name);
        Assert.Equal("COLA-05", first.Response.Sku);
        Assert.Equal(new MoneyDto("TJS", 1200), first.Response.Price);
        Assert.True(first.Response.TrackStock);
        Assert.False(first.Response.AllowNegativeStock);
        Assert.Equal(0, first.Response.StockOnHand);

        var product = await db.PosProducts.SingleAsync();
        Assert.Equal(first.Response.ProductId, product.ProductId);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task CreateProductAsync_PersistsReorderThreshold()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);

        var result = await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-rt") with { ReorderThreshold = 5 }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Response!.ReorderThreshold);
        Assert.Equal(5, (await db.PosProducts.SingleAsync()).ReorderThreshold);
    }

    [Fact]
    public async Task CreateProductAsync_RejectsNegativeReorderThreshold()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);

        var result = await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-neg") with { ReorderThreshold = -1 }, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateProductAsync_UpdatesReorderThreshold()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);

        var result = await service.UpdateProductAsync(TestIds.BranchId, product.ProductId, ActorStaffUserId,
            new UpdateProductRequest(TestIds.OrganizationId, product.CategoryId, product.Name, product.Sku, product.Price,
                TrackStock: true, AllowNegativeStock: false, IsActive: true, ReorderThreshold: 7),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Response!.ReorderThreshold);
    }

    [Fact]
    public async Task CreateProductAsync_RejectsDuplicateSkuInSameBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);

        await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId, ProductRequest(category.CategoryId, "product-001"), CancellationToken.None);
        var duplicate = await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-002") with { Name = "Cola Zero" },
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.False(duplicate.Conflict);
        Assert.Single(await db.PosProducts.ToListAsync());
    }

    [Fact]
    public async Task UpdateProductAsync_UpdatesFieldsAndCanDeactivate()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);
        var product = await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-001"),
            CancellationToken.None);
        Assert.NotNull(product.Response);
        await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.Response.ProductId, StockMovementTypeNames.Purchase, 12, "stock-001"),
            CancellationToken.None);

        var result = await service.UpdateProductAsync(
            TestIds.BranchId,
            product.Response.ProductId,
            ActorStaffUserId,
            new UpdateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                "Cola Zero",
                " cola-zero ",
                new MoneyDto("tjs", 1300),
                TrackStock: true,
                AllowNegativeStock: true,
                IsActive: false),
            CancellationToken.None);
        var catalog = await service.GetCatalogAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal("Cola Zero", result.Response.Name);
        Assert.Equal("COLA-ZERO", result.Response.Sku);
        Assert.Equal(new MoneyDto("TJS", 1300), result.Response.Price);
        Assert.True(result.Response.AllowNegativeStock);
        Assert.False(result.Response.IsActive);
        Assert.Equal(12, result.Response.StockOnHand);
        Assert.True(catalog.Succeeded);
        Assert.Empty(catalog.Response!);
    }

    [Fact]
    public async Task UpdateProductAsync_RejectsDuplicateSku()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);
        await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-001"),
            CancellationToken.None);
        var second = await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            ProductRequest(category.CategoryId, "product-002") with { Name = "Water 0.5", Sku = "WATER-05" },
            CancellationToken.None);
        Assert.NotNull(second.Response);

        var result = await service.UpdateProductAsync(
            TestIds.BranchId,
            second.Response.ProductId,
            ActorStaffUserId,
            new UpdateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                "Water 0.5",
                "cola-05",
                new MoneyDto("TJS", 600),
                TrackStock: true,
                AllowNegativeStock: false,
                IsActive: true),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
    }

    [Fact]
    public async Task CreateStockMovementAsync_PurchaseIncreasesDerivedStock()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        var request = new CreateStockMovementRequest(
            TestIds.OrganizationId,
            product.ProductId,
            StockMovementTypeNames.Purchase,
            24,
            new MoneyDto("TJS", 900),
            "initial stock",
            "stock-001");

        var result = await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var stock = await service.GetStockAsync(TestIds.OrganizationId, TestIds.BranchId, product.ProductId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(24, result.Response.QuantityDelta);
        Assert.Equal(StockMovementTypeNames.Purchase, result.Response.MovementType);
        Assert.True(stock.Succeeded);
        Assert.NotNull(stock.Response);
        Assert.Equal(24, stock.Response.StockOnHand);
        Assert.Single(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CreateStockMovementAsync_AdjustmentCanIncreaseOrDecreaseDerivedStock()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);

        await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Adjustment, 10, "adjust-up-001"),
            CancellationToken.None);
        var result = await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Adjustment, -3, "adjust-down-001"),
            CancellationToken.None);
        var stock = await service.GetStockAsync(TestIds.OrganizationId, TestIds.BranchId, product.ProductId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(stock.Response);
        Assert.Equal(7, stock.Response.StockOnHand);
        Assert.Equal(2, await db.StockMovements.CountAsync());
    }

    [Fact]
    public async Task CreateStockMovementAsync_ReplaysSameIdempotencyKeyAndRequest()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        var request = StockMovement(product.ProductId, StockMovementTypeNames.Purchase, 24, "stock-001");

        var first = await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.StockMovementId, second.Response.StockMovementId);
        Assert.Single(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CreateStockMovementAsync_RejectsQuantityDeltaZero()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);

        var result = await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Adjustment, 0, "stock-zero-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CreateStockMovementAsync_RejectsTrackedProductForWrongBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);

        var result = await service.CreateStockMovementAsync(
            OtherBranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Purchase, 24, "wrong-branch-001"),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task CreateStockMovementAsync_DropToReorderThreshold_EnqueuesLowStockAlertOnce()
    {
        await using var db = CreateDbContext();
        await SeedOwnerAsync(db);
        var recorder = new RecordingNotificationService();
        var service = CreateService(db, new EfLowStockNotifier(new EfOrganizationOwnerResolver(db), recorder, db));
        var category = await CreateCategoryAsync(service);
        var created = await service.CreateProductAsync(TestIds.BranchId, ActorStaffUserId,
            ProductRequest(category.CategoryId, "p-ls") with { ReorderThreshold = 3 }, CancellationToken.None);
        var productId = created.Response!.ProductId;

        // Purchase to 5 (above threshold) → no alert.
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId,
            StockMovement(productId, StockMovementTypeNames.Purchase, 5, "mv-purchase"), CancellationToken.None);
        Assert.Empty(recorder.Requests);

        // Adjust down to 3 (== threshold) → alert once.
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId,
            StockMovement(productId, StockMovementTypeNames.Adjustment, -2, "mv-adjust"), CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.LowStock, request.TemplateKey);
        Assert.Equal("3", request.Tokens["stockOnHand"]);
    }

    [Fact]
    public async Task CreateStockMovementAsync_RejectsNonStockProduct()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var category = await CreateCategoryAsync(service);
        var productResult = await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                "Day Pass",
                "DAY-PASS",
                new MoneyDto("TJS", 3000),
                trackStock: false,
                allowNegativeStock: false,
                "product-non-stock-001"),
            CancellationToken.None);
        Assert.True(productResult.Succeeded);
        Assert.NotNull(productResult.Response);

        var result = await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(productResult.Response.ProductId, StockMovementTypeNames.Purchase, 1, "non-stock-move-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task GetCatalogAsync_ReturnsActiveProductsWithDerivedStockOnHand()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var active = await CreateTrackedProductAsync(service);
        await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(active.ProductId, StockMovementTypeNames.Purchase, 24, "stock-001"),
            CancellationToken.None);
        await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(active.ProductId, StockMovementTypeNames.Sale, -2, "stock-002"),
            CancellationToken.None);
        var inactive = new PosProductEntity
        {
            ProductId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            CategoryId = active.CategoryId,
            Name = "Hidden Snack",
            Sku = "HIDDEN-SNACK",
            CurrencyCode = "TJS",
            PriceMinorUnits = 1000,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = false,
            CreatedAtUtc = Now
        };
        db.PosProducts.Add(inactive);
        await db.SaveChangesAsync();

        var catalog = await service.GetCatalogAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.True(catalog.Succeeded);
        Assert.NotNull(catalog.Response);
        var product = Assert.Single(catalog.Response);
        Assert.Equal(active.ProductId, product.ProductId);
        Assert.Equal(22, product.StockOnHand);
    }

    [Fact]
    public async Task GetStockMovementsAsync_ReturnsRecentMovementsFilteredByProduct()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        var otherProduct = await CreateTrackedProductAsync(service);
        var first = await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Purchase, 24, "stock-history-001"),
            CancellationToken.None);
        var latest = await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Adjustment, -2, "stock-history-002"),
            CancellationToken.None);
        await service.CreateStockMovementAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            StockMovement(otherProduct.ProductId, StockMovementTypeNames.Purchase, 10, "stock-history-003"),
            CancellationToken.None);
        Assert.NotNull(first.Response);
        Assert.NotNull(latest.Response);

        var firstEntity = await db.StockMovements.SingleAsync(movement => movement.StockMovementId == first.Response.StockMovementId);
        firstEntity.CreatedAtUtc = Now.AddMinutes(-10);
        var latestEntity = await db.StockMovements.SingleAsync(movement => movement.StockMovementId == latest.Response.StockMovementId);
        latestEntity.CreatedAtUtc = Now.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await service.GetStockMovementsAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            product.ProductId,
            limit: 1,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var movement = Assert.Single(result.Response);
        Assert.Equal(product.ProductId, movement.ProductId);
        Assert.Equal(-2, movement.QuantityDelta);
        Assert.Equal(StockMovementTypeNames.Adjustment, movement.MovementType);
    }

    [Fact]
    public async Task Purchase_SetsAvgCost_OnFirstReceipt()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
            TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase,
            10, new MoneyDto("TJS", 400), "поставка", "buy-1"), CancellationToken.None);
        var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
        Assert.Equal(400, entity.AvgCostMinorUnits);
    }

    [Fact]
    public async Task Purchase_RecomputesWeightedAverage()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        // 10 @ 400  → avg 400
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
            TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 10, new MoneyDto("TJS", 400), "p1", "buy-a"), CancellationToken.None);
        // + 30 @ 600 → (10*400 + 30*600)/40 = 550
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
            TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 30, new MoneyDto("TJS", 600), "p2", "buy-b"), CancellationToken.None);
        var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
        Assert.Equal(550, entity.AvgCostMinorUnits);
    }

    [Fact]
    public async Task Sale_DoesNotChangeAvgCost()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
            TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 10, new MoneyDto("TJS", 400), "p", "buy-c"), CancellationToken.None);
        await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
            TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Sale, -2, new MoneyDto("TJS", 0), "чек", "sale-c"), CancellationToken.None);
        var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
        Assert.Equal(400, entity.AvgCostMinorUnits);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfInventoryService CreateService(PlatformDbContext db, ILowStockNotifier? lowStockNotifier = null)
    {
        return new EfInventoryService(db, new FixedTimeProvider(Now), lowStockNotifier);
    }

    private static async Task SeedOwnerAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Name = "Central", CreatedAtUtc = Now });
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
        await db.SaveChangesAsync();
    }

    private static async Task<PosProductCategoryDto> CreateCategoryAsync(EfInventoryService service)
    {
        var result = await service.CreateCategoryAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new CreateProductCategoryRequest(TestIds.OrganizationId, $"Drinks {Guid.NewGuid():N}", $"category-{Guid.NewGuid():N}"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private static async Task<PosProductDto> CreateTrackedProductAsync(EfInventoryService service)
    {
        var category = await CreateCategoryAsync(service);
        var result = await service.CreateProductAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            ProductRequest(category.CategoryId, $"product-{Guid.NewGuid():N}") with
            {
                Name = $"Cola {Guid.NewGuid():N}",
                Sku = $"SKU-{Guid.NewGuid():N}"
            },
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private static CreateProductRequest ProductRequest(Guid categoryId, string idempotencyKey)
    {
        return new CreateProductRequest(
            TestIds.OrganizationId,
            categoryId,
            "Cola 0.5",
            "COLA-05",
            new MoneyDto("TJS", 1200),
            trackStock: true,
            allowNegativeStock: false,
            idempotencyKey);
    }

    private static CreateStockMovementRequest StockMovement(
        Guid productId,
        string movementType,
        int quantityDelta,
        string idempotencyKey)
    {
        return new CreateStockMovementRequest(
            TestIds.OrganizationId,
            productId,
            movementType,
            quantityDelta,
            new MoneyDto("TJS", 900),
            "stock test movement",
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
