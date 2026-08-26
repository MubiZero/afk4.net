using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests.Tournaments;

/// <summary>
/// События клуба по HTTP: кто может их заводить, кто видит, и что видит игрок. Правила самих
/// событий проверяет <see cref="EfTournamentServiceTests"/> — здесь только двери.
/// </summary>
public sealed class TournamentEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = TestPhones.Next();

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org,
            Slug = "club-" + org.ToString("N")[..8],
            Name = "Tournament Club",
            Status = OrganizationStatusNames.Active,
            PlanCode = "starter",
            LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(null, null, null, null)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "main",
            Name = "На Рудаки",
            City = "Душанбе",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Фаррух",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new SeededPlayer(org, branch, player, phone);
    }

    private static async Task<Guid> SeedPublishedTournamentAsync(
        PlatformApiFactory factory, SeededPlayer player, long entryFee = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tournamentId = Guid.NewGuid();
        db.Tournaments.Add(new TournamentEntity
        {
            TournamentId = tournamentId,
            OrganizationId = player.OrgId,
            BranchId = player.BranchId,
            Title = "Ночь Counter-Strike",
            Description = "Пять на пять",
            Discipline = "Counter-Strike",
            StartsAtUtc = Now.AddDays(3),
            EntryFeeMinorUnits = entryFee,
            CurrencyCode = "TJS",
            Capacity = 10,
            State = TournamentStateNames.Published,
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return tournamentId;
    }

    private static async Task DisableTournamentsAsync(PlatformApiFactory factory, Guid orgId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = orgId,
            FeatureKey = PlatformFeatureNames.Tournaments,
            IsEnabled = false,
            Reason = "tournament endpoint test",
            SetByPlatformAdminUserId = Guid.NewGuid(),
            SetAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task PlayerList_WithoutSignIn_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/branches/{player.BranchId:D}/tournaments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PlayerRegisters_AndAppearsInTheClubParticipants()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        var tournamentId = await SeedPublishedTournamentAsync(factory, player);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");

        var register = await client.PostAsync($"/api/me/tournaments/{tournamentId:D}/registration", null);

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<PlayerTournamentDto>();
        Assert.True(registered!.IsRegistered);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.TournamentRegistrations.SingleAsync();
        Assert.Equal(player.PlayerId, row.PlayerAccountId);
        Assert.Equal(TournamentRegistrationStateNames.Registered, row.State);
    }

    // Мест нет — это состояние, а не кривой запрос: приложение читает код и говорит словами.
    [Fact]
    public async Task RegisterTwice_AnswersConflictWithACode()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        var tournamentId = await SeedPublishedTournamentAsync(factory, player);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");

        await client.PostAsync($"/api/me/tournaments/{tournamentId:D}/registration", null);
        var again = await client.PostAsync($"/api/me/tournaments/{tournamentId:D}/registration", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<RefusalBody>();
        Assert.Equal(TournamentRefusalCodes.AlreadyRegistered, body!.Error);
    }

    [Fact]
    public async Task PlayerCancels_AndDisappearsFromTheClubParticipants()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        var tournamentId = await SeedPublishedTournamentAsync(factory, player);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");
        await client.PostAsync($"/api/me/tournaments/{tournamentId:D}/registration", null);

        var cancel = await client.DeleteAsync($"/api/me/tournaments/{tournamentId:D}/registration");

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var dto = await cancel.Content.ReadFromJsonAsync<PlayerTournamentDto>();
        Assert.False(dto!.IsRegistered);
    }

    // Выключенная платформой фича обязана блокировать запись на сервере, а не только прятать экран.
    [Fact]
    public async Task Register_RefusedWhenTournamentsAreOff()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        var tournamentId = await SeedPublishedTournamentAsync(factory, player);
        await DisableTournamentsAsync(factory, player.OrgId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");

        var response = await client.PostAsync($"/api/me/tournaments/{tournamentId:D}/registration", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.TournamentRegistrations.AnyAsync());
    }

    // Чужой филиал по прямой ссылке не отвечает даже расписанием.
    [Fact]
    public async Task PlayerList_ForAnotherClubsBranch_IsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        var player = await SeedPlayerAsync(factory, "1234");
        var stranger = await SeedPlayerAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");

        var response = await client.GetAsync($"/api/me/branches/{stranger.BranchId:D}/tournaments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClubEndpoints_AreClosedToStaffWithoutThePermission()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var create = await nonOwner.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/tournaments",
            new CreateTournamentRequest(
                TestIds.BranchId, "Ночь Counter-Strike", "Пять на пять", "Counter-Strike",
                Now.AddDays(3), 2000, 10));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Owner_CreatesPublishesAndSeesTheEvent()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

        var create = await owner.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/tournaments",
            new CreateTournamentRequest(
                TestIds.BranchId, "Ночь Counter-Strike", "Пять на пять", "Counter-Strike",
                Now.AddDays(3), 2000, 10));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TournamentDto>();
        Assert.Equal(TournamentStateNames.Draft, created!.State);

        var publish = await owner.PostAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/tournaments/{created.TournamentId:D}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var list = await owner.GetFromJsonAsync<TournamentDto[]>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/tournaments");
        var single = Assert.Single(list!);
        Assert.Equal(TournamentStateNames.Published, single.State);
        Assert.Equal(0, single.RegisteredCount);
    }

    private sealed record RefusalBody(string? Error);
}
