using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// «Кто я и где у меня счета». Единственный маршрут игрока, который работает без выбранного клуба,
/// — он их как раз и перечисляет.
/// </summary>
public sealed class MeEndpointTests
{
    [Fact]
    public async Task Me_ShowsThePersonAndEveryClubWithItsOwnMoney()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000401");
        var first = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Первый клуб");
        var second = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Второй клуб");

        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, first, "top_up", LedgerAccountTypeNames.Wallet, 10_000);
        var heldId = Guid.NewGuid();
        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, first, "reservation_hold", LedgerAccountTypeNames.Wallet, -2_500, ledgerEntryId: heldId);
        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, first, "postpaid_debt", LedgerAccountTypeNames.Debt, 1_200);
        await AddEndedSessionAsync(factory, first);
        await AddEndedSessionAsync(factory, first);

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, first.PlayerAccountId));

        var me = await client.GetFromJsonAsync<MeDto>("/api/me");

        Assert.Equal(person.PlatformPersonId, me!.Person.PlatformPersonId);
        Assert.Equal("+992900000401", me.Person.PhoneNumber);
        Assert.True(me.Person.PhoneVerified);
        Assert.False(me.Person.PinSet);
        Assert.False(me.Person.NetworkBanned);

        Assert.Equal(2, me.Clubs.Count);
        var firstClub = me.Clubs.Single(club => club.OrganizationId == first.OrganizationId);
        Assert.Equal("Первый клуб", firstClub.OrganizationName);
        Assert.Equal(first.PlayerAccountId, firstClub.PlayerAccountId);
        // Остаток не меняет смысла: холд из него уже вычтен, потому что холд и есть запись журнала.
        Assert.Equal(7_500, firstClub.WalletBalanceMinorUnits);
        Assert.Equal(2_500, firstClub.HeldMinorUnits);
        Assert.Equal(1_200, firstClub.DebtMinorUnits);
        Assert.Equal(2, firstClub.VisitCount);

        var secondClub = me.Clubs.Single(club => club.OrganizationId == second.OrganizationId);
        Assert.Equal("Второй клуб", secondClub.OrganizationName);
        Assert.Equal(0, secondClub.WalletBalanceMinorUnits);
        Assert.Equal(0, secondClub.HeldMinorUnits);
        Assert.Equal(0, secondClub.VisitCount);
    }

    [Fact]
    public async Task ReleasedHold_StopsBeingHeld()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000402");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);

        var heldId = Guid.NewGuid();
        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, club, "top_up", LedgerAccountTypeNames.Wallet, 5_000);
        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, club, "reservation_hold", LedgerAccountTypeNames.Wallet, -2_000, ledgerEntryId: heldId);
        await PlatformPersonTestData.AddLedgerEntryAsync(
            factory, club, "reversal", LedgerAccountTypeNames.Wallet, 2_000, reversesLedgerEntryId: heldId);

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, club.PlayerAccountId));

        var me = await client.GetFromJsonAsync<MeDto>("/api/me");
        var only = Assert.Single(me!.Clubs);
        Assert.Equal(5_000, only.WalletBalanceMinorUnits);
        Assert.Equal(0, only.HeldMinorUnits);
    }

    [Fact]
    public async Task Me_NeverMentionsSomebodyElsesClubs()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000403");
        var mine = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Мой клуб");
        var stranger = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000404");
        var theirs = await PlatformPersonTestData.AddClubAsync(factory, stranger.PlatformPersonId, "Чужой клуб");

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, mine.PlayerAccountId));

        var body = await client.GetStringAsync("/api/me");

        Assert.Contains("Мой клуб", body);
        Assert.DoesNotContain("Чужой клуб", body);
        Assert.DoesNotContain(theirs.OrganizationId.ToString(), body);
        Assert.DoesNotContain(stranger.PhoneNumber, body);
    }

    [Fact]
    public async Task Me_NeverHandsOutThePin()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000405");
        var club = await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId);
        await SetPinHashAsync(factory, person.PlatformPersonId, "секретный-хеш-пина");

        using var client = factory.CreateClient();
        Authorize(client, await IssueAsync(factory, person.PlatformPersonId, club.PlayerAccountId));

        var body = await client.GetStringAsync("/api/me");

        Assert.DoesNotContain("секретный-хеш-пина", body);
        Assert.DoesNotContain("pinHash", body, StringComparison.OrdinalIgnoreCase);
        // Признак «PIN задан» — это всё, что игроку и приложению нужно знать.
        Assert.Contains("\"pinSet\":true", body);
    }

    [Fact]
    public async Task Me_AnswersEvenWhenNoClubIsChosen()
    {
        await using var factory = new PlatformApiFactory();
        var person = await PlatformPersonTestData.AddPersonAsync(factory, "+992900000406");
        await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Первый");
        await PlatformPersonTestData.AddClubAsync(factory, person.PlatformPersonId, "Второй");

        using var client = factory.CreateClient();
        var tokens = await IssueAsync(factory, person.PlatformPersonId, (await FirstAccountIdAsync(factory, person.PlatformPersonId)));
        Authorize(client, tokens);
        // Клуб намеренно не назван и в токене не закреплён — этот маршрут и существует ради выбора.
        client.DefaultRequestHeaders.Remove(PlayerAuthenticationMiddleware.OrganizationHeader);

        var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<MeDto>())!.Clubs.Count);
    }

    private static void Authorize(HttpClient client, PlatformPersonSessionResponse tokens) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

    private static async Task<Guid> FirstAccountIdAsync(PlatformApiFactory factory, Guid platformPersonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.PlayerAccounts
            .Where(account => account.PlatformPersonId == platformPersonId)
            .Select(account => account.PlayerAccountId)
            .FirstAsync();
    }

    private static async Task<PlatformPersonSessionResponse> IssueAsync(
        PlatformApiFactory factory, Guid platformPersonId, Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformPersonTokenService>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        var account = await db.PlayerAccounts.SingleAsync(
            candidate => candidate.PlayerAccountId == playerAccountId);
        return await service.IssueAsync(person, account, CancellationToken.None);
    }

    private static async Task SetPinHashAsync(PlatformApiFactory factory, Guid platformPersonId, string pinHash)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(
            candidate => candidate.PlatformPersonId == platformPersonId);
        person.PinHash = pinHash;
        person.PinSetAtUtc = PlatformPersonTestData.Now;
        await db.SaveChangesAsync();
    }

    private static async Task AddEndedSessionAsync(PlatformApiFactory factory, PlayerAccountEntity account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = account.OrganizationId,
            BranchId = account.HomeBranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.Empty,
            PlayerKind = "member",
            PlayerAccountId = account.PlayerAccountId,
            TariffRuleVersionId = "v1",
            BillingMode = "prepaid_wallet",
            State = SessionStateNames.Ended,
            RequestedAtUtc = PlatformPersonTestData.Now,
            StartedAtUtc = PlatformPersonTestData.Now,
            EndedAtUtc = PlatformPersonTestData.Now.AddHours(1),
            UpdatedAtUtc = PlatformPersonTestData.Now.AddHours(1)
        });
        await db.SaveChangesAsync();
    }
}
