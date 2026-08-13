using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Что нужно приложению, чтобы игрок мог выбрать тариф: собственный филиал и цена выбора до того,
/// как бронь подтверждена. До этого филиал сервер знал про себя молча, и спросить прайс было не по
/// чему.
/// </summary>
public class PlayerBookingCatalogEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone, Guid TariffVersionId);

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        string pin,
        string branchName = "CyberX на Рудаки",
        int minimumBillableMinutes = 0,
        int roundingIncrementMinutes = 1)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var tariffId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        var phone = $"+99290000{player.ToString("N")[..4]}";

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Booking Test Org", CreatedAtUtc = Now });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branch, OrganizationId = org, Slug = $"b{branch:N}"[..12], Name = branchName,
            City = "Душанбе", CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player, OrganizationId = org, HomeBranchId = branch, DisplayName = "Игрок",
            PhoneNumber = phone, PreferredLocale = "ru", MarketingOptIn = false, IsActive = true, CreatedAtUtc = Now
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(), PlayerAccountId = player, OrganizationId = org,
            PhoneVerified = true, CreatedAtUtc = Now, UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        // Бронь с выбранным тарифом замораживает деньги, поэтому у игрока должен быть кошелёк:
        // без него сервер справедливо откажет «бронь только с деньгами».
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, PlayerAccountId = player,
            EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 50_000, CurrencyCode = "TJS", CreatedAtUtc = Now
        });
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId, OrganizationId = org, BranchId = branch, Name = "Ночной", IsActive = true, CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId, TariffId = tariffId, OrganizationId = org, BranchId = branch,
            VersionNumber = 1, CurrencyCode = "TJS", PricePerMinuteMinorUnits = 25,
            MinimumBillableMinutes = minimumBillableMinutes, RoundingIncrementMinutes = roundingIncrementMinutes,
            EffectiveFromUtc = Now.AddYears(-1)
        });
        await db.SaveChangesAsync();
        return new Seeded(org, branch, player, phone, tariffVersionId);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync("/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task Profile_CarriesTheHomeBranch_SoTheAppCanAskForThePriceList()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var profile = await client.GetFromJsonAsync<PlayerProfileDto>("/api/me/profile");

        Assert.Equal(seeded.BranchId, profile!.HomeBranchId);
        Assert.Equal("CyberX на Рудаки", profile.HomeBranchName);
    }

    [Fact]
    public async Task Quote_PricesTheBookingBeforeItIsMade()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(seeded.TariffVersionId, start, start.AddHours(2)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<ReservationQuoteDto>();
        Assert.Equal(120, quote!.RequestedMinutes);
        Assert.Equal(120, quote.BillableMinutes);
        Assert.Equal(3_000, quote.AmountMinorUnits);
        Assert.Equal("TJS", quote.CurrencyCode);
        Assert.Equal("Ночной", quote.TariffName);
    }

    // Ради этого расчёт и живёт на сервере: минимум и округление меняют сумму, а приложение,
    // умножающее минуты на цену, показало бы другую.
    [Fact]
    public async Task Quote_ShowsWhatTheMinimumAndRoundingDoToThePrice()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", minimumBillableMinutes: 60, roundingIncrementMinutes: 30);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(seeded.TariffVersionId, start, start.AddMinutes(70)));

        var quote = await response.Content.ReadFromJsonAsync<ReservationQuoteDto>();
        Assert.Equal(70, quote!.RequestedMinutes);
        Assert.Equal(90, quote.BillableMinutes);
        Assert.Equal(2_250, quote.AmountMinorUnits);
    }

    [Fact]
    public async Task Quote_RejectsATariffFromAnotherOrganization()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var start = Now.AddHours(3);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations/quote",
            new ReservationQuoteRequest(stranger.TariffVersionId, start, start.AddHours(1)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Booking_KeepsTheChosenTariffAndItsPrice()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, start, start.AddHours(1), null, seeded.TariffVersionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.Equal(seeded.TariffVersionId, created!.TariffVersionId);
        Assert.Equal(1_500, created.EstimatedCostMinorUnits);

        // И в списке броней цена та же — игрок видит, на что согласился, а не пересчёт по прайсу.
        var mine = await client.GetFromJsonAsync<List<PlayerReservationDto>>("/api/me/reservations");
        Assert.Equal(1_500, Assert.Single(mine!).EstimatedCostMinorUnits);
        Assert.Equal("Ночной", mine![0].TariffName);
    }

    [Fact]
    public async Task Booking_RejectsATariffFromAnotherOrganization()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var start = Now.AddHours(4);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, start, start.AddHours(1), null, stranger.TariffVersionId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.Reservations.AnyAsync(r => r.PlayerAccountId == mine.PlayerId));
    }
}
