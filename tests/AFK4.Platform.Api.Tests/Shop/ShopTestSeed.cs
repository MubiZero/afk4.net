using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
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

    // Seeds a staff user + credential + role assignment into the SAME org/branch the player was
    // seeded into (ShopTestSeed already created that branch), signs in via the staff route, and sets
    // the Bearer header. The Owner role carries ManageShopOrders; ShiftSupervisor deliberately lacks
    // it (for the 403 path).
    public static async Task AuthorizeStaffForBranchAsync(
        PlatformApiFactory factory,
        HttpClient client,
        Guid organizationId,
        Guid branchId,
        bool withShopPermission)
    {
        var email = $"staff-{Guid.NewGuid():N}@afk4.test";
        const string password = "Passw0rd!";
        var roleName = withShopPermission ? StaffRoleNames.Owner : StaffRoleNames.ShiftSupervisor;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var createdAt = DateTimeOffset.UtcNow;
            var staffUserId = Guid.NewGuid();

            // The player branch already exists; the staff sign-in route resolves the org row.
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = organizationId,
                Name = "Shop Org",
                CreatedAtUtc = createdAt
            });

            var user = new StaffUserEntity
            {
                StaffUserId = staffUserId,
                OrganizationId = organizationId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                DisplayName = "Shop Staff",
                IsActive = true,
                CreatedAtUtc = createdAt
            };
            user.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(user, password);
            db.StaffUsers.Add(user);

            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = staffUserId,
                OrganizationId = organizationId,
                BranchId = branchId,
                RoleName = roleName
            });

            await db.SaveChangesAsync();
        }

        var signIn = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(organizationId, email, password));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<StaffSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }
}
