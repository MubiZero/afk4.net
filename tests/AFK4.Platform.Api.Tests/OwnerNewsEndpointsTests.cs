using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.News;
using Xunit;

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
}
