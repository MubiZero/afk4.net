using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Оформление клуба. Поля <c>LogoUrl</c> и <c>AccentColor</c> читали публичная витрина и приложение
/// игрока с самого начала, а записать их было нечем: единственное присвоение во всём коде жило в
/// сидере для разработки.
/// </summary>
public sealed class OrganizationBrandingEndpointTests
{
    private static string Route => $"/api/organizations/{TestIds.OrganizationId:D}/branding";

    [Fact]
    public async Task PATCH_branding_StoresLogoAndColour()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PatchAsJsonAsync(
            Route,
            new UpdateOrganizationBrandingRequest("https://api.afk4.net/branding/presets/bolt.svg", "#c8ff00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
        Assert.Equal("https://api.afk4.net/branding/presets/bolt.svg", organization.LogoUrl);
        // Цвет нормализуется: в каталоге он лежит в одном регистре, а приходит как придётся.
        Assert.Equal("#C8FF00", organization.AccentColor);
    }

    [Fact]
    public async Task PATCH_branding_ClearsWithEmptyValues()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await client.PatchAsJsonAsync(Route, new UpdateOrganizationBrandingRequest("https://cdn.example/logo.png", "#fff"));
        var response = await client.PatchAsJsonAsync(Route, new UpdateOrganizationBrandingRequest(null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
        Assert.Null(organization.LogoUrl);
        Assert.Null(organization.AccentColor);
    }

    // Значение уезжает на экран игрока и в оболочку игрового ПК: там его подставляют в разметку,
    // поэтому `javascript:` и прочее адресом логотипа быть не может.
    [Theory]
    [InlineData("javascript:alert(1)", "#c8ff00")]
    [InlineData("data:image/svg+xml;base64,AAA", "#c8ff00")]
    [InlineData("/relative/logo.png", "#c8ff00")]
    [InlineData("https://cdn.example/logo.png", "красный")]
    [InlineData("https://cdn.example/logo.png", "#12345")]
    public async Task PATCH_branding_RejectsValuesItCannotRender(string logoUrl, string accentColor)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PatchAsJsonAsync(
            Route, new UpdateOrganizationBrandingRequest(logoUrl, accentColor));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PATCH_branding_WithoutPermission_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.PatchAsJsonAsync(
            Route, new UpdateOrganizationBrandingRequest("https://cdn.example/logo.png", "#c8ff00"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Прочитать оформление было нечем: форма настроек клуба открывалась бы пустой и затирала
    // выбранное в мастере установки.
    [Fact]
    public async Task GET_branding_ReturnsWhatWasSaved()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await client.PatchAsJsonAsync(
            Route, new UpdateOrganizationBrandingRequest("https://cdn.example/logo.png", "#30d158"));

        var branding = await client.GetFromJsonAsync<OrganizationBrandingDto>(Route);

        Assert.NotNull(branding);
        Assert.Equal("https://cdn.example/logo.png", branding.LogoUrl);
        Assert.Equal("#30D158", branding.AccentColor);
    }

    [Fact]
    public async Task GET_branding_WithoutPermission_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PATCH_branding_WithoutSession_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            Route, new UpdateOrganizationBrandingRequest("https://cdn.example/logo.png", "#c8ff00"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
