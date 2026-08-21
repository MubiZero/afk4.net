using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// «Приведи друга».
///
/// Игроки не регистрируются сами — аккаунт заводит клуб, — поэтому код называют уже в приложении,
/// отдельным действием. Платится приглашение не за код, а за первое настоящее пополнение друга:
/// код — это обещание прийти, деньги клуб отдаёт за приход.
/// </summary>
public class PlayerReferralEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Club(Guid OrgId, Guid BranchId);

    private static int phoneCounter;

    private static async Task<Club> SeedClubAsync(
        PlatformApiFactory factory,
        bool referralEnabled = true,
        long referrerBonus = 5_000,
        long inviteeBonus = 3_000,
        long minimumTopUp = 10_000,
        int claimWindowDays = 30,
        int maxRewardedPerReferrer = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Referral Org", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = "CyberX",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.OrganizationReferralSettings.Add(new OrganizationReferralSettingsEntity
        {
            OrganizationId = org,
            Enabled = referralEnabled,
            ReferrerBonusMinorUnits = referrerBonus,
            InviteeBonusMinorUnits = inviteeBonus,
            MinimumTopUpMinorUnits = minimumTopUp,
            ClaimWindowDays = claimWindowDays,
            MaxRewardedPerReferrer = maxRewardedPerReferrer,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return new Club(org, branch);
    }

    private static async Task<(Guid PlayerId, string Phone)> SeedPlayerAsync(
        PlatformApiFactory factory,
        Club club,
        string pin,
        int accountAgeDays = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var player = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        //
        // Счётчик, а не хеш Guid: хеш давал четырёхзначное пространство, и двое из трёх игроков
        // одного теста рано или поздно получали один номер. In-memory провайдер уникальный индекс
        // по телефону не исполняет, поэтому в личностях оказывались две строки с одним номером, а
        // вход по PIN, ищущий личность через SingleOrDefault, отвечал пятисоткой. Воспроизводилось
        // раз в несколько сотен прогонов — то есть только на CI и только у того, кто не виноват.
        var phone = $"+99290000{Interlocked.Increment(ref phoneCounter) % 10_000:D4}";

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = club.OrgId, HomeBranchId = club.BranchId,
            DisplayName = "Игрок", PhoneNumber = phone, PreferredLocale = "ru",
            IsActive = true, CreatedAtUtc = Now.AddDays(-accountAgeDays)
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return (player, phone);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync("/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    /// <summary>Пополнение так, как его делает касса клуба.</summary>
    private static async Task TopUpAsync(PlatformApiFactory factory, Club club, Guid playerId, long minorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var billing = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Billing.IBillingCommandService>();
        var result = await billing.CreditOnlineTopUpAsync(
            playerId,
            club.BranchId,
            new TopUpWalletRequest(club.OrgId, new MoneyDto("TJS", minorUnits), "test", Guid.NewGuid().ToString("N")),
            CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task<long> WalletAsync(PlatformApiFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.LedgerEntries
            .Where(entry =>
                entry.PlayerAccountId == playerId &&
                entry.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => entry.AmountMinorUnits);
    }

    [Fact]
    public async Task Referral_ShowsTheCodeAndTheClubsTerms()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, phone) = await SeedPlayerAsync(factory, club, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, club.OrgId, phone, "1234");

        var referral = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");

        Assert.True(referral!.Enabled);
        Assert.Equal(6, referral.Code!.Length);
        Assert.Equal(5_000, referral.ReferrerBonusMinorUnits);
        Assert.Equal(3_000, referral.InviteeBonusMinorUnits);
        Assert.True(referral.CanClaimCode);
        Assert.False(referral.HasClaimedCode);
    }

    // Код называют голосом и переписывают от руки: похожие знаки — это чужой бонус и спор
    // на стойке.
    [Fact]
    public async Task Code_AvoidsCharactersThatLookAlike()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, phone) = await SeedPlayerAsync(factory, club, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, club.OrgId, phone, "1234");

        var referral = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");

        Assert.DoesNotContain(referral!.Code!, character => "OI01".Contains(character));
    }

    [Fact]
    public async Task Code_StaysTheSameBetweenVisits()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, phone) = await SeedPlayerAsync(factory, club, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, club.OrgId, phone, "1234");

        var first = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");
        var second = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");

        Assert.Equal(first!.Code, second!.Code);
    }

    [Fact]
    public async Task Claim_RefusesTheCallersOwnCode()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, phone) = await SeedPlayerAsync(factory, club, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, club.OrgId, phone, "1234");
        var mine = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");

        var response = await client.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest(mine!.Code!));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("referral_own_code", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    // Ради этого приглашение и существует: друг пришёл и потратил деньги — платят обоим.
    [Fact]
    public async Task FirstTopUp_PaysBothSidesExactlyOnce()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (referrerId, referrerPhone) = await SeedPlayerAsync(factory, club, "1234");
        var (inviteeId, inviteePhone) = await SeedPlayerAsync(factory, club, "4321");

        using var referrerClient = factory.CreateClient();
        await AuthenticateAsync(referrerClient, club.OrgId, referrerPhone, "1234");
        var code = (await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var inviteeClient = factory.CreateClient();
        await AuthenticateAsync(inviteeClient, club.OrgId, inviteePhone, "4321");
        var claim = await inviteeClient.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest(code));
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);

        // Ввод кода сам по себе ничего не платит: пока друг не пришёл, платить не за что.
        Assert.Equal(0, await WalletAsync(factory, referrerId));
        Assert.Equal(0, await WalletAsync(factory, inviteeId));

        await TopUpAsync(factory, club, inviteeId, 20_000);

        Assert.Equal(5_000, await WalletAsync(factory, referrerId));
        Assert.Equal(23_000, await WalletAsync(factory, inviteeId));

        // Второе пополнение — обычные деньги, без второго бонуса.
        await TopUpAsync(factory, club, inviteeId, 20_000);

        Assert.Equal(5_000, await WalletAsync(factory, referrerId));
        Assert.Equal(43_000, await WalletAsync(factory, inviteeId));
    }

    // Иначе привести друга и положить ему один дирам — готовый способ печатать деньги клуба.
    [Fact]
    public async Task TopUpBelowTheMinimum_PaysNothingAndKeepsThePromise()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory, minimumTopUp: 10_000);
        var (referrerId, referrerPhone) = await SeedPlayerAsync(factory, club, "1234");
        var (inviteeId, inviteePhone) = await SeedPlayerAsync(factory, club, "4321");

        using var referrerClient = factory.CreateClient();
        await AuthenticateAsync(referrerClient, club.OrgId, referrerPhone, "1234");
        var code = (await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var inviteeClient = factory.CreateClient();
        await AuthenticateAsync(inviteeClient, club.OrgId, inviteePhone, "4321");
        await inviteeClient.PostAsJsonAsync("/api/me/referral/claim", new ClaimReferralCodeRequest(code));

        await TopUpAsync(factory, club, inviteeId, 500);

        Assert.Equal(0, await WalletAsync(factory, referrerId));
        Assert.Equal(500, await WalletAsync(factory, inviteeId));

        // Обещание не сгорело: следующее настоящее пополнение его закрывает.
        await TopUpAsync(factory, club, inviteeId, 20_000);

        Assert.Equal(5_000, await WalletAsync(factory, referrerId));
        Assert.Equal(23_500, await WalletAsync(factory, inviteeId));
    }

    [Fact]
    public async Task Claim_HappensOnlyOnceInAPlayersLife()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, firstPhone) = await SeedPlayerAsync(factory, club, "1111");
        var (_, secondPhone) = await SeedPlayerAsync(factory, club, "2222");
        var (_, inviteePhone) = await SeedPlayerAsync(factory, club, "4321");

        using var firstClient = factory.CreateClient();
        await AuthenticateAsync(firstClient, club.OrgId, firstPhone, "1111");
        var firstCode = (await firstClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var secondClient = factory.CreateClient();
        await AuthenticateAsync(secondClient, club.OrgId, secondPhone, "2222");
        var secondCode = (await secondClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var inviteeClient = factory.CreateClient();
        await AuthenticateAsync(inviteeClient, club.OrgId, inviteePhone, "4321");
        await inviteeClient.PostAsJsonAsync("/api/me/referral/claim", new ClaimReferralCodeRequest(firstCode));
        var again = await inviteeClient.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest(secondCode));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("referral_already_claimed", (await again.Content.ReadFromJsonAsync<ErrorBody>())!.Error);

        var mine = await inviteeClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");
        Assert.True(mine!.HasClaimedCode);
        Assert.False(mine.CanClaimCode);
    }

    // Приглашение — про новых игроков. Без окна давний завсегдатай однажды введёт код и
    // обналичит дружбу задним числом.
    [Fact]
    public async Task Claim_RefusesAnAccountOlderThanTheClaimWindow()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory, claimWindowDays: 30);
        var (_, referrerPhone) = await SeedPlayerAsync(factory, club, "1234");
        var (_, oldPhone) = await SeedPlayerAsync(factory, club, "4321", accountAgeDays: 90);

        using var referrerClient = factory.CreateClient();
        await AuthenticateAsync(referrerClient, club.OrgId, referrerPhone, "1234");
        var code = (await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var oldClient = factory.CreateClient();
        await AuthenticateAsync(oldClient, club.OrgId, oldPhone, "4321");
        var response = await oldClient.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest(code));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("referral_window_closed", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    [Fact]
    public async Task DisabledProgramme_ShowsNoCodeAndPaysNothing()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory, referralEnabled: false);
        var (_, phone) = await SeedPlayerAsync(factory, club, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, club.OrgId, phone, "1234");

        var referral = await client.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");
        Assert.False(referral!.Enabled);
        Assert.Null(referral.Code);

        var response = await client.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest("ABC234"));
        Assert.Equal("referral_disabled", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    // Приглашения не ходят между клубами: платит по ним конкретный клуб, и чужой код здесь
    // просто неизвестен.
    [Fact]
    public async Task Claim_DoesNotSeeACodeFromAnotherClub()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedClubAsync(factory);
        var other = await SeedClubAsync(factory);
        var (_, strangerPhone) = await SeedPlayerAsync(factory, other, "1111");
        var (_, myPhone) = await SeedPlayerAsync(factory, mine, "4321");

        using var strangerClient = factory.CreateClient();
        await AuthenticateAsync(strangerClient, other.OrgId, strangerPhone, "1111");
        var strangerCode = (await strangerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, myPhone, "4321");
        var response = await client.PostAsJsonAsync(
            "/api/me/referral/claim", new ClaimReferralCodeRequest(strangerCode));

        Assert.Equal("referral_unknown_code", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    // Лимит пригласившего не наказывает приглашённого: он ничего не нарушал и о лимите не знал.
    [Fact]
    public async Task PerReferrerCap_StopsPayingTheReferrerButStillPaysTheFriend()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory, maxRewardedPerReferrer: 1);
        var (referrerId, referrerPhone) = await SeedPlayerAsync(factory, club, "1234");
        var (firstId, firstPhone) = await SeedPlayerAsync(factory, club, "1111");
        var (secondId, secondPhone) = await SeedPlayerAsync(factory, club, "2222");

        using var referrerClient = factory.CreateClient();
        await AuthenticateAsync(referrerClient, club.OrgId, referrerPhone, "1234");
        var code = (await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        foreach (var (phone, pin) in new[] { (firstPhone, "1111"), (secondPhone, "2222") })
        {
            using var friend = factory.CreateClient();
            await AuthenticateAsync(friend, club.OrgId, phone, pin);
            await friend.PostAsJsonAsync("/api/me/referral/claim", new ClaimReferralCodeRequest(code));
        }

        await TopUpAsync(factory, club, firstId, 20_000);
        await TopUpAsync(factory, club, secondId, 20_000);

        Assert.Equal(5_000, await WalletAsync(factory, referrerId));
        Assert.Equal(23_000, await WalletAsync(factory, firstId));
        Assert.Equal(23_000, await WalletAsync(factory, secondId));
    }

    [Fact]
    public async Task Referral_CountsFriendsAndMoneyEarned()
    {
        await using var factory = new PlatformApiFactory();
        var club = await SeedClubAsync(factory);
        var (_, referrerPhone) = await SeedPlayerAsync(factory, club, "1234");
        var (friendId, friendPhone) = await SeedPlayerAsync(factory, club, "4321");

        using var referrerClient = factory.CreateClient();
        await AuthenticateAsync(referrerClient, club.OrgId, referrerPhone, "1234");
        var code = (await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral"))!.Code!;

        using var friendClient = factory.CreateClient();
        await AuthenticateAsync(friendClient, club.OrgId, friendPhone, "4321");
        await friendClient.PostAsJsonAsync("/api/me/referral/claim", new ClaimReferralCodeRequest(code));

        var beforeTopUp = await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");
        Assert.Equal(1, beforeTopUp!.InvitedCount);
        Assert.Equal(0, beforeTopUp.RewardedCount);
        Assert.Equal(0, beforeTopUp.EarnedMinorUnits);

        await TopUpAsync(factory, club, friendId, 20_000);

        var afterTopUp = await referrerClient.GetFromJsonAsync<PlayerReferralDto>("/api/me/referral");
        Assert.Equal(1, afterTopUp!.InvitedCount);
        Assert.Equal(1, afterTopUp.RewardedCount);
        Assert.Equal(5_000, afterTopUp.EarnedMinorUnits);
    }

    private sealed record ErrorBody(string Error);
}
