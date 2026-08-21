using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Бронь на компанию: несколько мест на одно время одним действием.
///
/// В киберклуб ходят компанией, а бронировали мы по одному месту. Мест здесь количество, а не
/// список: конкретную машину игроку в приложении не выбирают, её назначает клуб.
/// </summary>
public class PlayerGroupReservationEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone, Guid TariffVersionId);

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        string pin,
        long walletMinorUnits = 50_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var tariffId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99290000{(uint)player.GetHashCode() % 10_000:D4}";

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Group Test Org", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = "CyberX на Рудаки",
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = org, HomeBranchId = branch, DisplayName = "Игрок",
            PhoneNumber = phone, PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, PlayerAccountId = player,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = walletMinorUnits, CurrencyCode = "TJS", CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId, OrganizationId = org, BranchId = branch, Name = "Ночной", IsActive = true,
            CreatedAtUtc = Now
        });
        // 25 дирам за минуту: час стоит 1500, компания из четырёх — 6000.
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId, TariffId = tariffId, OrganizationId = org, BranchId = branch,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = 0, RoundingIncrementMinutes = 1, EffectiveFromUtc = Now.AddYears(-1)
        });
        // Файл про бронь на компанию, а не про правила приёма: филиал берёт её и без предоплаты.
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(org, branch, Now));
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new Seeded(org, branch, player, phone, tariffVersionId);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync("/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    /// <summary>
    /// Свободные деньги кошелька — то самое число, которое видит игрок. Холд уходит в минус
    /// обычной записью, снятие возвращается реверсом, поэтому сумма по кошельку и есть ответ на
    /// вопрос «сколько заморожено»: не надо знать про типы записей, достаточно посмотреть баланс.
    /// </summary>
    private static async Task<long> AvailableWalletAsync(PlatformApiFactory factory, Guid playerId)
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
    public async Task Group_BooksEverySeatUnderOneGroupAndHoldsMoneyForAllOfThem()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(4, start, start.AddHours(1), null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();
        Assert.Equal(4, group!.Reservations.Count);
        Assert.Equal(6_000, group.TotalEstimatedCostMinorUnits);
        Assert.All(group.Reservations, r => Assert.Equal(group.ReservationGroupId, r.ReservationGroupId));
        Assert.All(group.Reservations, r => Assert.Equal(1_500, r.EstimatedCostMinorUnits));
        // Деньги есть, значит компания подтверждена сразу и стойке подтверждать нечего.
        Assert.All(group.Reservations, r => Assert.Equal("confirmed", r.State));
        // Место назначает клуб — из приложения компания бронирует время, а не конкретные машины.
        Assert.All(group.Reservations, r => Assert.Null(r.SeatId));

        // 50 000 на кошельке минус 6 000, замороженных под компанию.
        Assert.Equal(44_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    // Посадить половину компании хуже, чем честно отказать: на четверых денег нет — не бронируется
    // ни одно место.
    [Fact]
    public async Task Group_RefusesWholeCompanyWhenTheWalletCoversOnlyPartOfIt()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", walletMinorUnits: 4_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(4, start, start.AddHours(1), null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("insufficient_funds", body!.Error);

        var mine = await client.GetFromJsonAsync<List<PlayerReservationDto>>("/api/me/reservations");
        Assert.Empty(mine!);
        // Ни одна монета не тронута: отказали до заморозки.
        Assert.Equal(4_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    [Fact]
    public async Task Group_RefusesAnImpossibleSeatCount()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        foreach (var seatCount in new[] { 0, -1, 99 })
        {
            var response = await client.PostAsJsonAsync(
                "/api/me/reservations/group",
                new CreatePlayerReservationGroupRequest(seatCount, start, start.AddHours(1), null, seeded.TariffVersionId));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            Assert.Equal("invalid_seat_count", body!.Error);
        }

        Assert.Equal(50_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    [Fact]
    public async Task CancellingTheGroup_ReleasesEveryHoldAtOnce()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var created = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(3, start, start.AddHours(1), null, seeded.TariffVersionId));
        var group = await created.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();
        Assert.Equal(45_500, await AvailableWalletAsync(factory, seeded.PlayerId));

        var cancelled = await client.DeleteAsync($"/api/me/reservations/group/{group!.ReservationGroupId}");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var rows = await cancelled.Content.ReadFromJsonAsync<List<PlayerReservationDto>>();
        Assert.Equal(3, rows!.Count);
        Assert.All(rows, r => Assert.Equal("cancelled", r.State));
        // Холд и его снятие гасят друг друга — свободных денег снова столько же, сколько было.
        Assert.Equal(50_000, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    // Один из компании передумал: отменяется одно место обычной отменой, остальные держатся.
    [Fact]
    public async Task CancellingOneSeat_LeavesTheRestOfTheCompanyBooked()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var created = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(3, start, start.AddHours(1), null, seeded.TariffVersionId));
        var group = await created.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();

        var dropped = group!.Reservations[0].ReservationId;
        var response = await client.DeleteAsync($"/api/me/reservations/{dropped}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Вернулись деньги ровно за одно место, остальные два остались замороженными.
        Assert.Equal(47_000, await AvailableWalletAsync(factory, seeded.PlayerId));

        var mine = await client.GetFromJsonAsync<List<PlayerReservationDto>>("/api/me/reservations");
        Assert.Equal(2, mine!.Count(r => r.State == "confirmed"));
        Assert.All(mine!, r => Assert.Equal(group.ReservationGroupId, r.ReservationGroupId));
    }

    [Fact]
    public async Task CancellingSomeoneElsesGroup_LooksExactlyLikeAMissingOne()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");

        using var strangerClient = factory.CreateClient();
        await AuthenticateAsync(strangerClient, stranger.OrgId, stranger.Phone, "4321");
        var start = Now.AddHours(4);
        var created = await strangerClient.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(2, start, start.AddHours(1), null, stranger.TariffVersionId));
        var group = await created.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");
        var response = await client.DeleteAsync($"/api/me/reservations/group/{group!.ReservationGroupId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // Чужие деньги остались замороженными: отказ не должен ничего трогать.
        Assert.Equal(47_000, await AvailableWalletAsync(factory, stranger.PlayerId));
    }

    // Без тарифа бронь считают на стойке — как и одиночная, компания встаёт без заморозки денег
    // и ждёт оператора.
    [Fact]
    public async Task Group_WithoutATariffWaitsForTheDeskAndHoldsNothing()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", walletMinorUnits: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/group",
            new CreatePlayerReservationGroupRequest(2, start, start.AddHours(1), null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<PlayerReservationGroupDto>();
        Assert.Null(group!.TotalEstimatedCostMinorUnits);
        Assert.All(group.Reservations, r => Assert.Equal("pending", r.State));
        // Кошелёк пуст и остаётся пустым: без тарифа замораживать нечего и не за что.
        Assert.Equal(0, await AvailableWalletAsync(factory, seeded.PlayerId));
    }

    [Fact]
    public async Task Quote_PricesTheWholeCompanyRatherThanOneSeat()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(seeded.TariffVersionId, start, start.AddHours(1), 4));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<ReservationQuoteDto>();
        Assert.Equal(4, quote!.SeatCount);
        Assert.Equal(6_000, quote.AmountMinorUnits);
        // Минуты остаются минутами одного места: умножается цена, а не время.
        Assert.Equal(60, quote.BillableMinutes);
    }

    private sealed record ErrorBody(string Error);
}
