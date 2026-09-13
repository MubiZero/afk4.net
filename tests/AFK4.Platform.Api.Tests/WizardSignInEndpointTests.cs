using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Вход мастера установки — тот, что происходит ДО того, как известна организация.
/// <see cref="WizardRouteContractTests"/> доказывает, что адреса существуют; здесь доказывается,
/// что они делают то, ради чего заведены: находят человека по всей сети, а не внутри одного клуба.
/// </summary>
public sealed class WizardSignInEndpointTests
{
    private const string Password = "Passw0rd!";

    private static readonly Guid SecondOrganizationId = Guid.Parse("7b1c9f24-3e58-4a6d-8c07-51f2a9d3b604");

    private static async Task SeedStaffAsync(
        PlatformApiFactory factory,
        Guid organizationId,
        string login,
        string? normalizedPhone = null,
        bool seedOrganization = false,
        string organizationName = "Клуб")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        if (seedOrganization)
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = organizationId,
                Name = organizationName,
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
            });
        }

        var staff = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserName = login,
            NormalizedUserName = login.ToUpperInvariant(),
            DisplayName = "Владелец",
            IsActive = true,
            Phone = normalizedPhone is null ? null : "+" + normalizedPhone,
            NormalizedPhone = normalizedPhone,
            PhoneVerifiedAtUtc = normalizedPhone is null
                ? null
                : DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, Password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SignInByPhone_WithoutOrganizationInThePath_ReturnsToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, TestIds.OrganizationId, "owner", normalizedPhone: "992937380070");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByPhone,
            new StaffSignInByPhoneRequest("+992 93 738-00-70", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(body);

        // Ровно то, ради чего маршрут корневой: организацию мастер узнаёт ИЗ ответа.
        Assert.Equal(TestIds.OrganizationId, body!.OrganizationId);
    }

    [Fact]
    public async Task SignInByPhone_WrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, TestIds.OrganizationId, "owner", normalizedPhone: "992937380070");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByPhone,
            new StaffSignInByPhoneRequest("+992 93 738-00-70", "WRONG"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignInByLogin_SingleClub_ReturnsToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, TestIds.OrganizationId, "owner@club.tj");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest("owner@club.tj", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(TestIds.OrganizationId, body!.OrganizationId);
    }

    [Fact]
    public async Task SignInByLogin_SameLoginInTwoClubs_ReturnsClubChoice()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(
            factory,
            TestIds.OrganizationId,
            "owner@club.tj",
            seedOrganization: true,
            organizationName: "Первый клуб");
        await SeedStaffAsync(
            factory,
            SecondOrganizationId,
            "owner@club.tj",
            seedOrganization: true,
            organizationName: "Второй клуб");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest("owner@club.tj", Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInChooseClubResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Clubs.Count);
        Assert.Contains(body.Clubs, club => club.OrganizationId == SecondOrganizationId);
    }

    [Fact]
    public async Task SignInByLogin_WrongPassword_ReturnsUnauthorizedRatherThanClubList()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(
            factory,
            TestIds.OrganizationId,
            "owner@club.tj",
            seedOrganization: true,
            organizationName: "Первый клуб");
        await SeedStaffAsync(
            factory,
            SecondOrganizationId,
            "owner@club.tj",
            seedOrganization: true,
            organizationName: "Второй клуб");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest("owner@club.tj", "WRONG"));

        // Список клубов — не справочник «где есть такой логин»: без верного пароля наружу не
        // уходит ни одного названия.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_WithChosenOrganizationInTheBody_ReturnsToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, TestIds.OrganizationId, "owner@club.tj");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignIn,
            new StaffSignInRequest(TestIds.OrganizationId, "owner@club.tj", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(TestIds.OrganizationId, body!.OrganizationId);
    }

    // Мастер не шлёт заголовков версии Organization Admin. Пока проверка совместимости висела на
    // пути входа, ответом был бы 426 «обнови панель» вместо работы — уже трижды наступали.
    [Fact]
    public async Task WizardSignIn_DoesNotDemandOrganizationAdminHeaders()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, TestIds.OrganizationId, "owner@club.tj");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest("owner@club.tj", Password));

        Assert.NotEqual(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }
}
