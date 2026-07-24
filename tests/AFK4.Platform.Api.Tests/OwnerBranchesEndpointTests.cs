using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerBranchesEndpointTests
{
    [Fact]
    public async Task GET_owner_branches_as_owner_returns_org_branches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var branches = await client.GetFromJsonAsync<OwnerBranchSummaryDto[]>("/api/owner/branches");

        Assert.NotNull(branches);
        Assert.Contains(branches!, b => b.BranchId == TestIds.BranchId);
    }

    [Fact]
    public async Task GET_owner_branches_as_cashier_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync("/api/owner/branches");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
