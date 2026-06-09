using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Inventory;

public sealed class InventoryAvailableInShellTests
{
    [Fact]
    public async Task CreateProduct_PersistsAvailableInShell_AndDtoExposesIt()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.PosProductCategories.Add(new PosProductCategoryEntity
        {
            CategoryId = categoryId, OrganizationId = orgId, BranchId = branchId,
            Name = "Drinks", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        await db.SaveChangesAsync();

        var service = new EfInventoryService(db, TimeProvider.System);
        var request = new CreateProductRequest(orgId, categoryId, "Cola", "COLA",
            new MoneyDto("TJS", 500), trackStock: true, allowNegativeStock: false,
            idempotencyKey: Guid.NewGuid().ToString("N")) { AvailableInShell = true };

        var result = await service.CreateProductAsync(branchId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Response!.AvailableInShell);
        var stored = await db.PosProducts.SingleAsync();
        Assert.True(stored.AvailableInShell);
    }
}
