using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Inventory;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Inventory;

public sealed class ProductBarcodeServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-26T10:00:00Z");
    private static readonly Guid ActorStaffUserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    [Fact]
    public async Task AddFirstBarcode_IsPrimary()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var product = await CreateTrackedProductAsync(service);

        var result = await service.AddProductBarcodeAsync(
            TestIds.BranchId, ActorStaffUserId, product.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "4601234567890"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Response!.IsPrimary);
    }

    [Fact]
    public async Task AddDuplicateCode_FailsWithConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var a = await CreateTrackedProductAsync(service);
        var b = await CreateTrackedProductAsync(service);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, a.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        var dup = await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, b.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        Assert.False(dup.Succeeded);
        Assert.Single(await db.ProductBarcodes.ToListAsync());
    }

    [Fact]
    public async Task AddSecondPrimary_DemotesFirst()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var p = await CreateTrackedProductAsync(service);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "222", IsPrimary: true), CancellationToken.None);

        var list = (await service.GetProductBarcodesAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, CancellationToken.None)).Response!;
        Assert.Equal("222", list.Single(x => x.IsPrimary).Code);
        Assert.Single(list, x => x.IsPrimary);
    }

    [Fact]
    public async Task DeletePrimary_PromotesOldestRemaining()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var p = await CreateTrackedProductAsync(service);
        var first = (await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None)).Response!;
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "222"), CancellationToken.None);

        await service.DeleteProductBarcodeAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, first.BarcodeId, CancellationToken.None);

        var list = (await service.GetProductBarcodesAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, CancellationToken.None)).Response!;
        Assert.Single(list);
        Assert.True(list[0].IsPrimary);
    }

    [Fact]
    public async Task AddToForeignOrgProduct_Fails()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var p = await CreateTrackedProductAsync(service);

        var result = await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(Guid.NewGuid() /* чужая org */, "111"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.ProductBarcodes.ToListAsync());
    }

    [Fact]
    public async Task Catalog_IncludesBarcodes_PrimaryFirst()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await SeedOwnerAsync(db);
        var p = await CreateTrackedProductAsync(service);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "AAA"), CancellationToken.None);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "BBB", IsPrimary: true), CancellationToken.None);

        var catalog = (await service.GetCatalogAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None)).Response!;
        var dto = catalog.Single(x => x.ProductId == p.ProductId);
        Assert.Equal(new[] { "BBB", "AAA" }, dto.Barcodes);
    }

    // --- хелперы (зеркалят EfInventoryServiceTests) ---

    private static PlatformDbContext CreateDbContext()
    {
        return new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
    }

    private static EfInventoryService CreateService(PlatformDbContext db) =>
        new(db, new FixedTimeProvider(Now));

    private static async Task SeedOwnerAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId,
            Name = "Central", CreatedAtUtc = Now
        });
        var ownerId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = TestIds.OrganizationId,
            UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = "owner@club.example",
            PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
            RoleName = StaffRoleNames.Owner
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
            new CreateProductRequest(
                TestIds.OrganizationId,
                category.CategoryId,
                $"Cola {Guid.NewGuid():N}",
                $"SKU-{Guid.NewGuid():N}",
                new MoneyDto("TJS", 1200),
                trackStock: true,
                allowNegativeStock: false,
                idempotencyKey: Guid.NewGuid().ToString("N")),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        return result.Response;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
