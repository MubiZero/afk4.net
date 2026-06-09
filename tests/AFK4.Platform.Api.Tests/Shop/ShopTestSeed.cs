using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Shop;

internal sealed record SeededShop(
    Guid OrganizationId,
    Guid BranchId,
    Guid ColaProductId,
    string Phone,
    string Pin);

internal static class ShopTestSeed
{
    public static async Task<SeededShop> SeedActivePlayerWithProductsAsync(
        PlatformApiFactory factory, long walletMinor = 5000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var seat = Guid.NewGuid();
        var session = Guid.NewGuid();
        var colaProduct = Guid.NewGuid();
        var hiddenProduct = Guid.NewGuid();
        const string pin = "1234";
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

        db.Sessions.Add(new SessionEntity
        {
            SessionId = session,
            OrganizationId = org,
            BranchId = branch,
            SeatId = seat,
            PlayerAccountId = player,
            State = "active",
            PlayerKind = "registered",
            TariffRuleVersionId = "v1",
            Version = 1
        });

        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = colaProduct,
            OrganizationId = org,
            BranchId = branch,
            CategoryId = Guid.NewGuid(),
            Name = "Cola",
            Sku = "COLA",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = true,
            AvailableInShell = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = hiddenProduct,
            OrganizationId = org,
            BranchId = branch,
            CategoryId = Guid.NewGuid(),
            Name = "Hidden",
            Sku = "HIDDEN",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = true,
            AvailableInShell = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = org,
            BranchId = branch,
            ProductId = colaProduct,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = 10,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 0,
            Reason = "seed",
            CreatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        if (walletMinor != 0)
        {
            db.LedgerEntries.Add(BillingEntryFactory.Create(
                org, branch, player, null, null,
                LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
                walletMinor, 0, "TJS", "seed", "seed", null, Guid.Empty, DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync();
        return new SeededShop(org, branch, colaProduct, phone, pin);
    }

    public static async Task AuthenticatePlayerAsync(HttpClient client, SeededShop seeded)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new AFK4.Shared.Contracts.Players.PlayerSignInRequest(seeded.OrganizationId, seeded.Phone, seeded.Pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<AFK4.Shared.Contracts.Players.PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }
}
