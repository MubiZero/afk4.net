using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Первое действие в клубе, где человека ещё нет. Клуб не заводит человека заранее — он открывает
/// ему счёт ровно тогда, когда тот впервые что-то просит, и ни секундой раньше: чтение чужой
/// витрины счёта не создаёт.
/// </summary>
public sealed class PlayerFirstActionEndpointTests
{
    [Fact]
    public async Task TopUp_InAClubHeHasNeverVisited_OpensTheAccountAndGoesThrough()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000701");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var opened = await db.PlayerAccounts.SingleAsync(
            account => account.OrganizationId == newClub.OrganizationId);
        Assert.Equal(person.PlatformPersonId, opened.PlatformPersonId);
        Assert.True(opened.CreatedFromApp);
        Assert.Equal(newClub.BranchId, opened.HomeBranchId);
        // Счёт открылся нулём: намерение пополнить — это ещё не деньги.
        Assert.False(await db.LedgerEntries.AnyAsync(
            entry => entry.PlayerAccountId == opened.PlayerAccountId));
    }

    [Fact]
    public async Task Booking_InAClubHeHasNeverVisited_NoLongerAnswersThatThePlayerIsUnknown()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000702");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var starts = DateTimeOffset.UtcNow.AddDays(1);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, starts, starts.AddHours(1), null));

        // Дальше бронь может не выйти по залу или тарифу, но «тебя тут нет» — уже не ответ.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.PlayerAccounts.AnyAsync(
            account => account.OrganizationId == newClub.OrganizationId
                && account.PlatformPersonId == person.PlatformPersonId));
    }

    [Fact]
    public async Task JustLookingAround_OpensNothing()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000703");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var response = await client.GetAsync("/api/me/profile");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlayerAccounts.AnyAsync(
            account => account.OrganizationId == newClub.OrganizationId));
    }

    [Fact]
    public async Task TwoFirstActions_OpenOneAccount()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000704");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);
        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());

        await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));
        await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(7_000, "TJS", "counter"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.PlayerAccounts.CountAsync(
            account => account.OrganizationId == newClub.OrganizationId));
    }

    [Fact]
    public async Task FirstAction_AdoptsTheCardTheOperatorMadeByHand()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000705");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var counterCard = await PlayerClubMembershipServiceTests.AddCounterCardAsync(
            factory, newClub, person.PhoneNumber, "Фаррух с PS5");
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var only = await db.PlayerAccounts.SingleAsync(
            account => account.OrganizationId == newClub.OrganizationId);
        Assert.Equal(counterCard, only.PlayerAccountId);
    }

    [Fact]
    public async Task NonsenseRequest_OpensNothing()
    {
        await using var factory = FactoryWithAllFeatures();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000706");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(0, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Отклонённая попытка не должна оставлять клубу карточку гостя, который так и не пришёл.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlayerAccounts.AnyAsync(
            account => account.OrganizationId == newClub.OrganizationId));
    }

    [Fact]
    public async Task ClubThatSwitchedTheFeatureOff_OpensNothing()
    {
        await using var factory = FactoryWith(new EverythingOffEntitlements());
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000707");
        var home = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        var newClub = await PlayerClubMembershipServiceTests.AddClubWithBranchAsync(factory);
        using var client = factory.CreateClient();
        await AuthorizeAsync(factory, client, person.PlatformPersonId, home);

        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, newClub.OrganizationId.ToString());
        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent", new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlayerAccounts.AnyAsync(
            account => account.OrganizationId == newClub.OrganizationId));
    }

    private static PlatformApiFactory FactoryWithAllFeatures() =>
        FactoryWith(AlwaysEnabledOrganizationEntitlements.Instance);

    private static PlatformApiFactory FactoryWith(IOrganizationEntitlements entitlements) =>
        new(extraServices: services =>
        {
            services.RemoveAll<IOrganizationEntitlements>();
            services.AddSingleton(entitlements);
        });

    /// <summary>Клуб, у которого онлайн-функции выключены целиком.</summary>
    private sealed class EverythingOffEntitlements : IOrganizationEntitlements
    {
        public Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<AFK4.Shared.Contracts.Platform.Features.OrganizationFeatureStateDto>> DescribeAsync(
            Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>
    /// Домашний клуб и вход в него. Сам HttpClient создаёт тест: PlatformApiFactory привязывает
    /// свою базу к async-контексту вызывающего, и созданный во вложенном методе клиент смотрел бы
    /// в чужую.
    /// </summary>
    private static async Task AuthorizeAsync(
        PlatformApiFactory factory,
        HttpClient client,
        Guid platformPersonId,
        PlayerClubMembershipServiceTests.SeededClub homeClub)
    {
        Guid homeAccountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var account = new PlayerAccountEntity
            {
                PlayerAccountId = Guid.NewGuid(),
                OrganizationId = homeClub.OrganizationId,
                HomeBranchId = homeClub.BranchId,
                PlatformPersonId = platformPersonId,
                DisplayName = "Домашний клуб",
                IsActive = true,
                CreatedAtUtc = PlatformPersonTestData.Now
            };
            db.PlayerAccounts.Add(account);
            await db.SaveChangesAsync();
            homeAccountId = account.PlayerAccountId;
        }

        await using var tokenScope = factory.Services.CreateAsyncScope();
        var tokenDb = tokenScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = await tokenScope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>()
            .IssueAsync(
                await tokenDb.PlatformPersons.SingleAsync(p => p.PlatformPersonId == platformPersonId),
                await tokenDb.PlayerAccounts.SingleAsync(a => a.PlayerAccountId == homeAccountId),
                CancellationToken.None);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
}
