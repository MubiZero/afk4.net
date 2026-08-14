using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchProfileEndpointTests
{
    private static UpdateBranchProfileRequest FullRequest(Guid orgId) => new(
        OrganizationId: orgId,
        Name: "AFK4 Центр",
        City: "Душанбе",
        Description: "Лучший клуб",
        Address: "ул. Рудаки, 1",
        Phone: "+992900000000",
        Telegram: "afk4club",
        Website: "https://afk4.net",
        Instagram: "afk4.club",
        LogoUrl: null,
        LogoMediaId: null,
        TimeZone: "Asia/Dushanbe",
        Locale: "ru",
        WorkingHours: Enumerable.Range(1, 7)
            .Select(d => new BranchWorkingHoursDayDto(d, d == 7, d == 7 ? null : "10:00", d == 7 ? null : "23:00"))
            .ToList());

    [Fact]
    public async Task Patch_WithBranchManager_PersistsAllFields()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<BranchProfileDto>();
        Assert.NotNull(dto);
        Assert.Equal("AFK4 Центр", dto!.Name);
        Assert.Equal("Лучший клуб", dto.Description);
        Assert.Equal("afk4club", dto.Telegram);
        Assert.Equal("afk4.club", dto.Instagram);
        Assert.Equal("Asia/Dushanbe", dto.TimeZone);
        Assert.Equal(7, dto.WorkingHours.Count);
        Assert.True(dto.WorkingHours.Single(d => d.DayOfWeek == 7).IsClosed);
    }

    [Fact]
    public async Task Get_AfterPatch_ReturnsPersistedProfile()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        await client.PatchAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        var dto = await client.GetFromJsonAsync<BranchProfileDto>($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile");

        Assert.NotNull(dto);
        Assert.Equal("ул. Рудаки, 1", dto!.Address);
        Assert.Equal("23:00", dto.WorkingHours.Single(d => d.DayOfWeek == 1).CloseTime);
    }

    [Fact]
    public async Task Get_DefaultBranch_ReturnsSevenWorkingHourDays()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var dto = await client.GetFromJsonAsync<BranchProfileDto>($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile");
        Assert.NotNull(dto);
        Assert.Equal(7, dto!.WorkingHours.Count);
    }

    /// Фото зала и точка на карте — то, из чего собрана витрина клуба в приложении игрока.
    [Fact]
    public async Task Patch_PersistsShowcaseCoverAndCoordinates()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var request = FullRequest(TestIds.OrganizationId) with
        {
            CoverImageUrl = "https://cdn.example/hall.jpg",
            Latitude = 38.5598,
            Longitude = 68.7870
        };
        await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", request);

        var dto = await client.GetFromJsonAsync<BranchProfileDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile");

        Assert.Equal("https://cdn.example/hall.jpg", dto!.CoverImageUrl);
        Assert.Equal(38.5598, dto.Latitude);
        Assert.Equal(68.7870, dto.Longitude);
    }

    /// Галерея зала: обложка отвечает «как тут выглядит», остальные фото — «а что ещё».
    [Fact]
    public async Task Patch_PersistsHallGalleryInTheGivenOrder()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var request = FullRequest(TestIds.OrganizationId) with
        {
            Photos =
            [
                new BranchPhotoDto("https://cdn.example/vip.jpg", null),
                new BranchPhotoDto("https://cdn.example/bar.jpg", null)
            ]
        };
        await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", request);

        var dto = await client.GetFromJsonAsync<BranchProfileDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile");

        Assert.Equal(
            new[] { "https://cdn.example/vip.jpg", "https://cdn.example/bar.jpg" },
            dto!.Photos.Select(photo => photo.Url).ToArray());
    }

    /// Больше десятка фото зала никто не пролистает, а каждое — трафик игрока на витрине.
    [Fact]
    public async Task Patch_TooManyPhotos_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile",
            FullRequest(TestIds.OrganizationId) with
            {
                Photos = Enumerable.Range(1, 11)
                    .Select(index => new BranchPhotoDto($"https://cdn.example/{index}.jpg", null))
                    .ToList()
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// Точка за пределами глобуса на карте выглядит поломкой карты, а не опечаткой в форме.
    [Fact]
    public async Task Patch_CoordinatesOutsideTheGlobe_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile",
            FullRequest(TestIds.OrganizationId) with { Latitude = 138.0, Longitude = 68.7870 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// Одна широта без долготы поставила бы клуб на нулевой меридиан — в Атлантику.
    [Fact]
    public async Task Patch_HalfOfACoordinatePair_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile",
            FullRequest(TestIds.OrganizationId) with { Latitude = 38.5598 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_InvalidWorkingHours_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);

        var bad = FullRequest(TestIds.OrganizationId) with
        {
            WorkingHours = new[] { new BranchWorkingHoursDayDto(1, false, "22:00", "10:00") }
        };
        var response = await client.PatchAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", bad);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithCashier_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
