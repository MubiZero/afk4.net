using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.AspNetCore.Identity;
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
        var phone = $"+99291{player.ToString("N")[..7]}";

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

        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        credential.PasswordHash =
            new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);

        await db.SaveChangesAsync();
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
