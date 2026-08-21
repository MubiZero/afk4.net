using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerLoyaltyEndpointsTests
{
    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99291{(uint)player.GetHashCode() % 10_000_000:D7}";

        // IOrganizationEntitlements.IsEnabledAsync anchors on the Organizations row: without it,
        // the org is "unknown" and every feature (including loyalty) resolves to disabled.
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org,
            Name = "Loyalty Test Org",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "test-branch",
            Name = "Test Branch",
            City = "Dushanbe",
            CreatedAtUtc = DateTimeOffset.UtcNow
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
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new SeededPlayer(org, branch, player, phone);
    }

    private static async Task SeedLoyaltyAsync(PlatformApiFactory factory, SeededPlayer p)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = p.OrgId,
            TopUpEnabled = true,
            TopUpPercentBasisPoints = 500,
            ShopEnabled = false,
            ShopPercentBasisPoints = 0,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            PlayerAccountId = p.PlayerId,
            EntryType = LedgerEntryTypeNames.Cashback,
            AccountType = "wallet",
            AmountMinorUnits = 120,
            CurrencyCode = "TJS",
            Reason = "topup_cashback",
            CreatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            PlayerAccountId = p.PlayerId,
            EntryType = LedgerEntryTypeNames.Cashback,
            AccountType = "wallet",
            AmountMinorUnits = 80,
            CurrencyCode = "TJS",
            Reason = "topup_cashback",
            CreatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        await db.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(
        HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new AFK4.Shared.Contracts.Players.PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<AFK4.Shared.Contracts.Players.PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task SeedManyCashbackAsync(PlatformApiFactory factory, SeededPlayer p, int count, long amountMinor)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = p.OrgId,
            TopUpEnabled = true,
            TopUpPercentBasisPoints = 500,
            ShopEnabled = false,
            ShopPercentBasisPoints = 0,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        for (var i = 0; i < count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(),
                OrganizationId = p.OrgId,
                BranchId = p.BranchId,
                PlayerAccountId = p.PlayerId,
                EntryType = LedgerEntryTypeNames.Cashback,
                AccountType = "wallet",
                AmountMinorUnits = amountMinor,
                CurrencyCode = "TJS",
                Reason = "topup_cashback",
                CreatedByStaffUserId = Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetLoyalty_TotalEarnedSumsAllCashback_RecentCappedAt20()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedManyCashbackAsync(factory, p, count: 21, amountMinor: 10);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync("/api/me/loyalty");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerLoyaltyDto>();
        Assert.NotNull(dto);
        Assert.Equal(210, dto!.TotalEarned.MinorUnits);
        Assert.Equal("TJS", dto.TotalEarned.CurrencyCode);
        Assert.Equal(20, dto.Recent.Count);
    }

    [Fact]
    public async Task GetLoyalty_ReturnsRatesEarnedAndRecent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedLoyaltyAsync(factory, p);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync("/api/me/loyalty");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerLoyaltyDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.TopUpEnabled);
        Assert.Equal(500, dto.TopUpPercentBasisPoints);
        Assert.False(dto.ShopEnabled);
        Assert.Equal(200, dto.TotalEarned.MinorUnits);
        Assert.Equal("TJS", dto.TotalEarned.CurrencyCode);
        Assert.Equal(2, dto.Recent.Count);
    }
}
