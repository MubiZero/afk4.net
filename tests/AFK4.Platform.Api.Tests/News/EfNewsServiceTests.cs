using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.News;
using AFK4.Shared.Contracts.News;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.News;

public sealed class EfNewsServiceTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid OtherOrg = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private static EfNewsService NewService(PlatformDbContext db) =>
        new(db, new FakeTimeProvider(Now));

    private static async Task SeedBranchAsync(PlatformDbContext db, Guid org, Guid branch)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "b",
            Name = "Branch",
            City = "Dushanbe",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static CreateNewsItemRequest ValidCreate(Guid? branchId = null) =>
        new(branchId, "Title", "Body", null, true, null, null);

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsItem()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal("Title", result.Item!.Title);
        Assert.Equal(1, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_TrimsAndRejectsEmptyTitle()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate() with { Title = "   " }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsNonHttpImageUrl()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate() with { ImageUrl = "ftp://x/y.png" }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvertedWindow()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org,
            ValidCreate() with { PublishAtUtc = Now.AddHours(2), ExpiresAtUtc = Now.AddHours(1) }, default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_RejectsForeignBranch()
    {
        using var db = NewDb();
        var result = await NewService(db).CreateAsync(Org, ValidCreate(branchId: Branch), default);
        Assert.Equal(NewsMutationOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_AcceptsOwnBranch()
    {
        using var db = NewDb();
        await SeedBranchAsync(db, Org, Branch);
        var result = await NewService(db).CreateAsync(Org, ValidCreate(branchId: Branch), default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal(Branch, result.Item!.BranchId);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundForForeignOrg()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        var update = new UpdateNewsItemRequest(null, "X", "Y", null, false, null, null);
        var result = await NewService(db).UpdateAsync(OtherOrg, created.Item!.Id, update, default);
        Assert.Equal(NewsMutationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesFields()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        var update = new UpdateNewsItemRequest(null, "New", "NewBody", "https://x/y.png", false, null, null);
        var result = await NewService(db).UpdateAsync(Org, created.Item!.Id, update, default);
        Assert.Equal(NewsMutationOutcome.Success, result.Outcome);
        Assert.Equal("New", result.Item!.Title);
        Assert.False(result.Item.IsPublished);
        Assert.Equal("https://x/y.png", result.Item.ImageUrl);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwnAndRejectsForeign()
    {
        using var db = NewDb();
        var created = await NewService(db).CreateAsync(Org, ValidCreate(), default);
        Assert.Equal(NewsMutationOutcome.NotFound, await NewService(db).DeleteAsync(OtherOrg, created.Item!.Id, default));
        Assert.Equal(NewsMutationOutcome.Success, await NewService(db).DeleteAsync(Org, created.Item!.Id, default));
        Assert.Equal(0, await db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task ListForPlayer_AppliesPublishedWindowAndBranchFilters()
    {
        using var db = NewDb();
        var svc = NewService(db);
        await svc.CreateAsync(Org, ValidCreate(), default);
        await svc.CreateAsync(Org, ValidCreate() with { IsPublished = false }, default);
        await svc.CreateAsync(Org, ValidCreate() with { PublishAtUtc = Now.AddHours(1) }, default);
        await svc.CreateAsync(Org, ValidCreate() with { ExpiresAtUtc = Now.AddHours(-1) }, default);
        await SeedBranchAsync(db, Org, Branch);
        await svc.CreateAsync(Org, ValidCreate(branchId: Branch), default);

        var orgWide = await svc.ListForPlayerAsync(Org, homeBranchId: null, default);
        Assert.Single(orgWide);

        var branchPlayer = await svc.ListForPlayerAsync(Org, homeBranchId: Branch, default);
        Assert.Equal(2, branchPlayer.Count);
    }

    [Fact]
    public async Task ListForPlayer_PublishedAtUtcFallsBackToCreatedAt()
    {
        using var db = NewDb();
        var svc = NewService(db);
        await svc.CreateAsync(Org, ValidCreate(), default);
        var items = await svc.ListForPlayerAsync(Org, null, default);
        Assert.Equal(Now, items[0].PublishedAtUtc);
    }
}

file sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
