using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shop;
using AFK4.Platform.Api.Tests.Shop;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

/// <summary>
/// Проверяет, что выключенная фича действительно блокирует запись на сервере (не только прячет
/// экран) и что клубское приложение может узнать список включённых фич. Гейт, который отвечает
/// 403 уже после записи в базу, — не гейт, поэтому каждый "refused" тест проверяет пустоту базы.
/// </summary>
public sealed class FeatureGateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    // Seeds an Organization (required for IOrganizationEntitlements.IsEnabledAsync to resolve
    // anything other than "unknown org -> everything off"), a Branch, and a verified-phone player.
    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99291{player.ToString("N")[..7]}";

        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org,
            Slug = "club-" + org.ToString("N")[..8],
            Name = "Feature Gate Club",
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
            Slug = "branch-" + branch.ToString("N")[..8],
            Name = "Test Branch",
            City = "Dushanbe",
            CreatedAtUtc = Now
        });

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });

        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);

        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, player, phone);
    }

    private static async Task<Guid> SeedSeatAsync(PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Main",
            SortOrder = 10,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = zoneId,
            Name = "PC-01",
            SortOrder = 10,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return seatId;
    }

    private static async Task DisableFeatureAsync(PlatformApiFactory factory, Guid orgId, string featureKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.OrganizationFeatureOverrides.Add(new OrganizationFeatureOverrideEntity
        {
            OrganizationFeatureOverrideId = Guid.NewGuid(),
            OrganizationId = orgId,
            FeatureKey = featureKey,
            IsEnabled = false,
            Reason = "feature gate test",
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

    private sealed record FeatureDisabledBody(string? Error, string? Code, string? FeatureKey);

    // ---- POST /api/me/reservations (online_booking) ----

    [Fact]
    public async Task Booking_Refused_WhenOnlineBookingDisabled()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var seatId = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        await DisableFeatureAsync(factory, p.OrgId, PlatformFeatureNames.OnlineBooking);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = Now.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FeatureDisabledBody>();
        Assert.NotNull(body);
        Assert.Equal(PlatformFeatureNames.DisabledCode, body!.Code);
        Assert.Equal(PlatformFeatureNames.OnlineBooking, body.FeatureKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.Reservations.AnyAsync());
    }

    [Fact]
    public async Task Booking_Allowed_WhenEnabled()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var seatId = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = Now.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.NotNull(dto);
        Assert.Equal(seatId, dto!.SeatId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.Reservations.AnyAsync(r => r.ReservationId == dto.ReservationId));
    }

    // ---- POST /api/me/wallet/top-up-intent (online_topup) ----

    [Fact]
    public async Task TopUp_Refused_WhenOnlineTopUpDisabled()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await DisableFeatureAsync(factory, p.OrgId, PlatformFeatureNames.OnlineTopUp);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS", "counter"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FeatureDisabledBody>();
        Assert.NotNull(body);
        Assert.Equal(PlatformFeatureNames.DisabledCode, body!.Code);
        Assert.Equal(PlatformFeatureNames.OnlineTopUp, body.FeatureKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.PaymentIntents.AnyAsync());
    }

    // ---- POST /api/me/shop/orders + GET /api/me/shop/catalog (player_shop) ----

    [Fact]
    public async Task ShopOrder_Refused_WhenPlayerShopDisabled()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await DisableFeatureAsync(factory, seeded.OrganizationId, PlatformFeatureNames.PlayerShop);
        using var client = factory.CreateClient();
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(
                new[] { new ShopOrderLineInput(seeded.ColaProductId, 1) },
                "feature-gate-shop-order-001"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FeatureDisabledBody>();
        Assert.NotNull(body);
        Assert.Equal(PlatformFeatureNames.DisabledCode, body!.Code);
        Assert.Equal(PlatformFeatureNames.PlayerShop, body.FeatureKey);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.ShopOrders.AnyAsync());
    }

    [Fact]
    public async Task ShopCatalog_Refused_WhenPlayerShopDisabled()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await DisableFeatureAsync(factory, seeded.OrganizationId, PlatformFeatureNames.PlayerShop);
        using var client = factory.CreateClient();
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.GetAsync("/api/me/shop/catalog");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FeatureDisabledBody>();
        Assert.NotNull(body);
        Assert.Equal(PlatformFeatureNames.DisabledCode, body!.Code);
        Assert.Equal(PlatformFeatureNames.PlayerShop, body.FeatureKey);
    }

    // ---- Cashback accrual (loyalty) ----

    private static async Task SeedLoyaltySettingsAsync(PlatformApiFactory factory, Guid orgId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = orgId,
            TopUpEnabled = true,
            TopUpPercentBasisPoints = 500,
            ShopEnabled = false,
            ShopPercentBasisPoints = 0,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Loyalty_NotAccrued_WhenLoyaltyDisabled()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedLoyaltySettingsAsync(factory, p.OrgId);
        await DisableFeatureAsync(factory, p.OrgId, PlatformFeatureNames.Loyalty);

        await using var scope = factory.Services.CreateAsyncScope();
        var accrualService = scope.ServiceProvider.GetRequiredService<ILoyaltyAccrualService>();

        var entry = await accrualService.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, p.OrgId, p.BranchId, p.PlayerId, sessionId: null,
            sourceMinorUnits: 5_000, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.Null(entry);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback));
    }

    [Fact]
    public async Task Loyalty_Accrued_WhenEnabled()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedLoyaltySettingsAsync(factory, p.OrgId);

        await using var scope = factory.Services.CreateAsyncScope();
        var accrualService = scope.ServiceProvider.GetRequiredService<ILoyaltyAccrualService>();

        var entry = await accrualService.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, p.OrgId, p.BranchId, p.PlayerId, sessionId: null,
            sourceMinorUnits: 5_000, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryTypeNames.Cashback, entry!.EntryType);
        Assert.Equal(250, entry.AmountMinorUnits); // floor(5000 * 500 / 10000) = 250

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();
        Assert.True(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback));
    }

    // ---- GET /api/me/features ----

    [Fact]
    public async Task Features_ListsOnlyEnabledKeys()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await DisableFeatureAsync(factory, p.OrgId, PlatformFeatureNames.PlayerShop);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync("/api/me/features");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<EnabledFeaturesDto>();
        Assert.NotNull(dto);
        Assert.DoesNotContain(PlatformFeatureNames.PlayerShop, dto!.Features);
        Assert.Contains(PlatformFeatureNames.OnlineBooking, dto.Features);
        Assert.Contains(PlatformFeatureNames.OnlineTopUp, dto.Features);
        Assert.Contains(PlatformFeatureNames.Loyalty, dto.Features);
        Assert.Equal(PlatformFeatureNames.All.Count - 1, dto.Features.Count);
    }

    [Fact]
    public async Task Features_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/features");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
