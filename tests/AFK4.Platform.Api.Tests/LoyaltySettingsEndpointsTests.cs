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

        var put = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(
                TopUpEnabled: true, TopUpPercentBasisPoints: 500,
                ShopEnabled: false, ShopPercentBasisPoints: 0,
                SessionEnabled: true, SessionPercentBasisPoints: 300,
                CashbackCapMinorUnits: 5000, MinimumSourceMinorUnits: 1000));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var get = await owner.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<LoyaltySettingsDto>();
        Assert.True(dto!.TopUpEnabled);
        Assert.Equal(500, dto.TopUpPercentBasisPoints);
        Assert.False(dto.ShopEnabled);
        Assert.True(dto.SessionEnabled);
        Assert.Equal(300, dto.SessionPercentBasisPoints);
        Assert.Equal(5000, dto.CashbackCapMinorUnits);
        Assert.Equal(1000, dto.MinimumSourceMinorUnits);
    }

    [Fact]
    public async Task Put_RejectsOutOfRangePercent()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var put = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(true, 10001, false, 0, false, 0, 0, 0));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_RejectsOutOfRangeSessionPercent()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var put = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(false, 0, false, 0, true, 10001, 0, 0));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_RejectsNegativeLimits()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var put = await owner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(true, 500, false, 0, false, 0, -1, 0));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_ForbiddenForNonOwner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var put = await nonOwner.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings",
            new UpdateLoyaltySettingsRequest(true, 500, false, 0, false, 0, 0, 0));
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Get_DefaultsToAllDisabledWhenNoRow()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var get = await owner.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/loyalty-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var dto = await get.Content.ReadFromJsonAsync<LoyaltySettingsDto>();
        Assert.False(dto!.TopUpEnabled);
        Assert.Equal(0, dto.TopUpPercentBasisPoints);
        Assert.False(dto.ShopEnabled);
        Assert.Equal(0, dto.ShopPercentBasisPoints);
    }
}
