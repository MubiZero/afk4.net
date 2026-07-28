using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EfLowStockNotifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T10:00:00Z");
    private static readonly Guid OrgId = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private static readonly Guid BranchId = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task SeedOwnerAsync(PlatformDbContext db, string? ownerEmail = "owner@club.example")
    {
        var ownerId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrgId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity { BranchId = BranchId, OrganizationId = OrgId, Name = "Central Branch", CreatedAtUtc = Now });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = OrgId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = ownerEmail, PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = OrgId, BranchId = BranchId, RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedProductAsync(PlatformDbContext db, int reorderThreshold, int stockOnHand, bool trackStock = true)
    {
        var productId = Guid.NewGuid();
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = productId, OrganizationId = OrgId, BranchId = BranchId, CategoryId = Guid.NewGuid(),
            Name = "Cola 0.5", Sku = "COLA-05", CurrencyCode = "TJS", PriceMinorUnits = 1200,
            TrackStock = trackStock, ReorderThreshold = reorderThreshold, IsActive = true, CreatedAtUtc = Now
        });
        if (stockOnHand != 0)
        {
            db.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ProductId = productId,
                MovementType = "purchase", QuantityDelta = stockOnHand, CurrencyCode = "TJS", UnitCostMinorUnits = 500,
                Reason = "seed", CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = Now
            });
        }
        await db.SaveChangesAsync();
        return productId;
    }

    private static EfLowStockNotifier NewNotifier(PlatformDbContext db, RecordingNotificationService recorder) =>
        new(new EfOrganizationOwnerResolver(db), recorder, db);

    [Fact]
    public async Task EvaluateProductsAsync_StockAtOrBelowThreshold_EnqueuesOperationalLowStockAndSetsFlag()
    {
        await using var db = NewContext();
        await SeedOwnerAsync(db);
        var productId = await SeedProductAsync(db, reorderThreshold: 10, stockOnHand: 8);
        var recorder = new RecordingNotificationService();

        await NewNotifier(db, recorder).EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.LowStock, request.TemplateKey);
        Assert.Equal(NotificationCategory.Operational, request.Category);
        Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
        Assert.Equal($"inventory-low-stock:{productId:N}:0", request.IdempotencyKey);
        Assert.Equal("Cola 0.5", request.Tokens["productName"]);
        Assert.Equal("8", request.Tokens["stockOnHand"]);
        Assert.Equal("10", request.Tokens["threshold"]);
        Assert.True((await db.PosProducts.SingleAsync()).LowStockAlerted);
    }

    [Fact]
    public async Task EvaluateProductsAsync_AlreadyAlerted_DoesNotReAlert()
    {
        await using var db = NewContext();
        await SeedOwnerAsync(db);
        var productId = await SeedProductAsync(db, reorderThreshold: 10, stockOnHand: 8);
        var recorder = new RecordingNotificationService();
        var notifier = NewNotifier(db, recorder);

        await notifier.EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None);
        await notifier.EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None);

        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task EvaluateProductsAsync_ThresholdZeroOrUntracked_NoOp()
    {
        await using var db = NewContext();
        await SeedOwnerAsync(db);
        var noThreshold = await SeedProductAsync(db, reorderThreshold: 0, stockOnHand: 1);
        var untracked = await SeedProductAsync(db, reorderThreshold: 10, stockOnHand: 1, trackStock: false);
        var recorder = new RecordingNotificationService();

        await NewNotifier(db, recorder).EvaluateProductsAsync(OrgId, BranchId, [noThreshold, untracked], CancellationToken.None);

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task EvaluateProductsAsync_RestockAboveThreshold_ReArmsAndNextDipUsesNewCycleKey()
    {
        await using var db = NewContext();
        await SeedOwnerAsync(db);
        var productId = await SeedProductAsync(db, reorderThreshold: 10, stockOnHand: 8);
        var recorder = new RecordingNotificationService();
        var notifier = NewNotifier(db, recorder);

        await notifier.EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None); // alert (cycle 0)

        // Restock above threshold → re-arm.
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ProductId = productId,
            MovementType = "purchase", QuantityDelta = 20, CurrencyCode = "TJS", UnitCostMinorUnits = 500,
            Reason = "restock", CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await notifier.EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None); // re-arm, no alert

        var product = await db.PosProducts.SingleAsync();
        Assert.False(product.LowStockAlerted);
        Assert.Equal(1, product.LowStockCycle);

        // Dip below again → alert with the new cycle key.
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ProductId = productId,
            MovementType = "sale", QuantityDelta = -25, CurrencyCode = "TJS", UnitCostMinorUnits = 500,
            Reason = "sale", CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await notifier.EvaluateProductsAsync(OrgId, BranchId, [productId], CancellationToken.None);

        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal($"inventory-low-stock:{productId:N}:1", recorder.Requests[1].IdempotencyKey);
    }
}
