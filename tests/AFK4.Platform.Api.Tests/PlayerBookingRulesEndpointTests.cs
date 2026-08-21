using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Те же настройки филиала глазами игрока: приложение показывает «так решил клуб» именно ими.
///
/// Игрок видит только то, что касается его самого: нужна ли предоплата ему, сколько броней
/// доступно ему. Ни одного поля про других игроков и ни одного числа из внутренней кухни клуба.
/// </summary>
public sealed class PlayerBookingRulesEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        int endedVisits = 0,
        BranchBookingSettingsEntity? settings = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99290000{(uint)player.GetHashCode() % 10_000:D4}";

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Клуб", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = "Филиал",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = org, HomeBranchId = branch, DisplayName = "Игрок",
            PhoneNumber = phone, PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Now
        });

        for (var index = 0; index < endedVisits; index++)
        {
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, SeatId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(), PlayerAccountId = player, PlayerKind = "member",
                State = SessionStateNames.Ended, RequestedAtUtc = Now.AddDays(-10 + index),
                StartedAtUtc = Now.AddDays(-10 + index), EndedAtUtc = Now.AddDays(-10 + index).AddHours(2)
            });
        }

        if (settings is not null)
        {
            settings.BranchId = branch;
            settings.OrganizationId = org;
            db.BranchBookingSettings.Add(settings);
        }

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, PlayerPinTestData.DefaultPin);
        return new Seeded(org, branch, player, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, PlayerPinTestData.DefaultPin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/me/branches/{seeded.BranchId:D}/booking-rules");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewGuest_SeesTheDefaultRulesOfAnUnconfiguredBranch()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone);

        var rules = await client.GetFromJsonAsync<PlayerBookingRulesDto>(
            $"/api/me/branches/{seeded.BranchId:D}/booking-rules");

        Assert.NotNull(rules);
        Assert.Equal(seeded.BranchId, rules.BranchId);
        Assert.Equal(BranchBookingAcceptanceModes.Auto, rules.AcceptanceMode);
        Assert.Equal(15, rules.RespondWithinMinutes);
        Assert.True(rules.PrepaymentRequired);
        Assert.Equal(0, rules.ActiveReservations);
        Assert.Equal(1, rules.MaxActiveReservations);
        Assert.Equal(20, rules.HoldSeatAfterStartMinutes);
    }

    [Fact]
    public async Task RegularGuest_SeesNoPrepaymentAndNoLimit()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, endedVisits: 3);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone);

        var rules = await client.GetFromJsonAsync<PlayerBookingRulesDto>(
            $"/api/me/branches/{seeded.BranchId:D}/booking-rules");

        Assert.NotNull(rules);
        Assert.False(rules.PrepaymentRequired);
        Assert.Null(rules.MaxActiveReservations);
    }

    [Fact]
    public async Task ClosedBranch_SaysSoInsteadOfPretendingBookingsWork()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, settings: new BranchBookingSettingsEntity
        {
            AcceptanceMode = BranchBookingAcceptanceModes.Off,
            RespondWithinMinutes = 45,
            RequirePrepaymentFromNewGuests = false,
            MaxActiveReservationsForNewGuests = 4,
            RegularAfterVisits = 2,
            HoldSeatAfterStartMinutes = 30,
            KeepPrepaymentOnNoShow = true,
            UpdatedAtUtc = Now,
            UpdatedByStaffUserId = Guid.NewGuid()
        });
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone);

        var rules = await client.GetFromJsonAsync<PlayerBookingRulesDto>(
            $"/api/me/branches/{seeded.BranchId:D}/booking-rules");

        Assert.NotNull(rules);
        Assert.Equal(BranchBookingAcceptanceModes.Off, rules.AcceptanceMode);
        Assert.Equal(45, rules.RespondWithinMinutes);
        Assert.False(rules.PrepaymentRequired);
        Assert.Equal(4, rules.MaxActiveReservations);
        Assert.Equal(30, rules.HoldSeatAfterStartMinutes);
    }

    [Fact]
    public async Task GuestWithoutAnAccountInThisClub_StillSeesTheRules()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000801");
        var club = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory, "Незнакомый клуб");
        using var client = factory.CreateClient();
        await AuthenticateAsPersonAsync(factory, client, person.PlatformPersonId, club.OrganizationId);

        var rules = await client.GetFromJsonAsync<PlayerBookingRulesDto>(
            $"/api/me/branches/{club.BranchId:D}/booking-rules");

        // Правила приёма нужнее всего как раз тому, у кого счёта ещё нет: предоплата, ручной
        // приём и потолок броней — это то, что он должен узнать до первой брони, а не после.
        Assert.NotNull(rules);
        Assert.Equal(club.BranchId, rules.BranchId);
        Assert.True(rules.PrepaymentRequired);
        Assert.Equal(0, rules.ActiveReservations);
        Assert.Equal(1, rules.MaxActiveReservations);

        // Чтение витрины счёта не заводит: карточки-призраки стойке не нужны.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PlayerAccounts.AnyAsync(
            account => account.OrganizationId == club.OrganizationId));
    }

    [Fact]
    public async Task GuestWithoutAnAccount_AskingAboutAnotherClubsBranch_IsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000802");
        var club = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory, "Незнакомый клуб");
        var stranger = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory, "Чужой клуб");
        using var client = factory.CreateClient();
        await AuthenticateAsPersonAsync(factory, client, person.PlatformPersonId, club.OrganizationId);

        var response = await client.GetAsync($"/api/me/branches/{stranger.BranchId:D}/booking-rules");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_TheRulesStayClosed_EvenForANamedClub()
    {
        await using var factory = new PlatformApiFactory();
        var club = await PlatformPersonTestData.AddClubWithoutAccountsAsync(factory, "Незнакомый клуб");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, club.OrganizationId.ToString());

        var response = await client.GetAsync($"/api/me/branches/{club.BranchId:D}/booking-rules");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Человек опознан, клуб назван, счёта в нём нет — состояние, в котором приложение открывает
    /// клуб впервые.
    /// </summary>
    private static async Task AuthenticateAsPersonAsync(
        PlatformApiFactory factory,
        HttpClient client,
        Guid platformPersonId,
        Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokens = await scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>()
            .IssueAsync(
                await db.PlatformPersons.SingleAsync(p => p.PlatformPersonId == platformPersonId),
                pinnedAccount: null,
                CancellationToken.None);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        client.DefaultRequestHeaders.Add(
            PlayerAuthenticationMiddleware.OrganizationHeader, organizationId.ToString());
    }

    [Fact]
    public async Task BranchOfAnotherClub_IsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory);
        var stranger = await SeedAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone);

        var response = await client.GetAsync($"/api/me/branches/{stranger.BranchId:D}/booking-rules");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
