using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.News;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerNewsEndpointsTests
{
    private static CreateNewsItemRequest Valid() =>
        new(null, "Hello", "World", null, true, null, null);

    [Fact]
    public async Task CreateListPatchDelete_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var create = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/news", Valid());
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<NewsItemDto>();

        var list = await owner.GetFromJsonAsync<NewsItemDto[]>($"/api/organizations/{TestIds.OrganizationId:D}/news");
        Assert.Single(list!);

        var patch = await owner.PatchAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/news/{created!.Id}",
            new UpdateNewsItemRequest(null, "Edited", "Body2", null, false, null, null));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var edited = await patch.Content.ReadFromJsonAsync<NewsItemDto>();
        Assert.Equal("Edited", edited!.Title);
        Assert.False(edited.IsPublished);

        var delete = await owner.DeleteAsync($"/api/organizations/{TestIds.OrganizationId:D}/news/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await owner.GetFromJsonAsync<NewsItemDto[]>($"/api/organizations/{TestIds.OrganizationId:D}/news");
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task Create_RejectsEmptyTitle()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var create = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/news",
            new CreateNewsItemRequest(null, "   ", "Body", null, true, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Patch_ReturnsNotFoundForUnknownId()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var patch = await owner.PatchAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/news/{Guid.NewGuid()}",
            new UpdateNewsItemRequest(null, "X", "Y", null, true, null, null));
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task Create_ForbiddenForNonOwner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var create = await nonOwner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/news", Valid());
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Branches_ReturnsOwnOrgBranches()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var branches = await owner.GetFromJsonAsync<OwnerBranchSummaryDto[]>($"/api/organizations/{TestIds.OrganizationId:D}/branches");
        Assert.NotEmpty(branches!);
    }

    // Роли выдаются по филиалам: управляющий одного филиала пишет новости своего филиала, но не
    // соседнего и не всей сети — и чужих не видит.
    [Fact]
    public async Task ABranchManager_ManagesOnlyTheirOwnBranchNews()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, AFK4.Platform.Api.Identity.OrganizationRoleNames.BranchManager);
        var neighbour = Guid.NewGuid();
        var neighbourNews = Guid.NewGuid();
        var networkNews = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Data.PlatformDbContext>();
            var at = DateTimeOffset.Parse("2026-09-28T00:00:00Z");
            db.Branches.Add(new AFK4.Platform.Api.Data.BranchEntity
            {
                BranchId = neighbour, OrganizationId = TestIds.OrganizationId, Name = "Соседний", CreatedAtUtc = at
            });
            db.NewsItems.AddRange(
                new AFK4.Platform.Api.Data.NewsItemEntity
                {
                    Id = neighbourNews, OrganizationId = TestIds.OrganizationId, BranchId = neighbour,
                    Title = "Соседи", Body = "Их турнир", IsPublished = true, CreatedAtUtc = at, UpdatedAtUtc = at
                },
                new AFK4.Platform.Api.Data.NewsItemEntity
                {
                    Id = networkNews, OrganizationId = TestIds.OrganizationId, BranchId = null,
                    Title = "Вся сеть", Body = "От владельца", IsPublished = true, CreatedAtUtc = at, UpdatedAtUtc = at
                });
            await db.SaveChangesAsync();
        }
        var news = $"/api/organizations/{TestIds.OrganizationId:D}/news";

        var own = await client.PostAsJsonAsync(news, Valid() with { BranchId = TestIds.BranchId });
        var toNeighbour = await client.PostAsJsonAsync(news, Valid() with { BranchId = neighbour });
        var toNetwork = await client.PostAsJsonAsync(news, Valid());
        var editNeighbour = await client.PatchAsJsonAsync($"{news}/{neighbourNews}",
            new UpdateNewsItemRequest(neighbour, "Правка", "Чужое", null, true, null, null));
        var moveOwnToNetwork = await client.PatchAsJsonAsync($"{news}/{(await own.Content.ReadFromJsonAsync<NewsItemDto>())!.Id}",
            new UpdateNewsItemRequest(null, "Hello", "World", null, true, null, null));
        var deleteNetwork = await client.DeleteAsync($"{news}/{networkNews}");
        var list = await client.GetFromJsonAsync<NewsItemDto[]>(news);
        var scopeDto = await client.GetFromJsonAsync<NewsScopeDto>($"{news}/scope");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, toNeighbour.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, toNetwork.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editNeighbour.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, moveOwnToNetwork.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteNetwork.StatusCode);
        Assert.All(list!, item => Assert.Equal(TestIds.BranchId, item.BranchId));
        Assert.Equal([TestIds.BranchId], scopeDto!.Branches.Select(branch => branch.BranchId));
        Assert.False(scopeDto.CanPublishToAllBranches);
    }

    [Fact]
    public async Task TheOwner_MayPublishToTheWholeNetwork()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var scope = await owner.GetFromJsonAsync<NewsScopeDto>($"/api/organizations/{TestIds.OrganizationId:D}/news/scope");

        Assert.True(scope!.CanPublishToAllBranches);
        Assert.Contains(scope.Branches, branch => branch.BranchId == TestIds.BranchId);
    }
}
