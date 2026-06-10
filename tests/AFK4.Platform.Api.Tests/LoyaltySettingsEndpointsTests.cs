using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Loyalty;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class LoyaltySettingsEndpointsTests
{
    [Fact]
    public async Task PutThenGet_RoundTripsOrgLoyaltySettings()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var put = await owner.PostAsJsonAsync("/api/owner/loyalty-settings",
            new UpdateLoyaltySettingsRequest(TopUpEnabled: true, TopUpPercentBasisPoints: 500, ShopEnabled: false, ShopPercentBasisPoints: 0));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await owner.GetAsync("/api/owner/loyalty-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<LoyaltySettingsDto>();
        Assert.True(dto!.TopUpEnabled);
        Assert.Equal(500, dto.TopUpPercentBasisPoints);
        Assert.False(dto.ShopEnabled);
    }

    [Fact]
    public async Task Put_RejectsOutOfRangePercent()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var put = await owner.PostAsJsonAsync("/api/owner/loyalty-settings",
            new UpdateLoyaltySettingsRequest(true, 10001, false, 0));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_ForbiddenForNonOwner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var put = await nonOwner.PostAsJsonAsync("/api/owner/loyalty-settings",
            new UpdateLoyaltySettingsRequest(true, 500, false, 0));
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Get_DefaultsToAllDisabledWhenNoRow()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var get = await owner.GetAsync("/api/owner/loyalty-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<LoyaltySettingsDto>();
        Assert.False(dto!.TopUpEnabled);
        Assert.Equal(0, dto.TopUpPercentBasisPoints);
        Assert.False(dto.ShopEnabled);
        Assert.Equal(0, dto.ShopPercentBasisPoints);
    }
}
