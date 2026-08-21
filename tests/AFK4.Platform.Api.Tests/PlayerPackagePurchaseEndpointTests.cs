using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Пакет часов, купленный из приложения. Смысл в предоплате: игрок платит вперёд и получает время
/// дешевле, а клуб — деньги до визита. Поэтому покупка не ждёт открытой смены и не зовёт оператора.
/// </summary>
public class PlayerPackagePurchaseEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Seeded(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone, Guid PackageDefinitionId);

    private static async Task<Seeded> SeedAsync(
        PlatformApiFactory factory,
        string pin,
        long walletMinorUnits = 50_000,
        long packagePriceMinorUnits = 40_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var packageDefinitionId = Guid.NewGuid();
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99290000{(uint)player.GetHashCode() % 10_000:D4}";

        db.Organizations.Add(new OrganizationEntity { OrganizationId = org, Name = "Package Test Org", CreatedAtUtc = Now });
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
        if (walletMinorUnits > 0)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(), OrganizationId = org, BranchId = branch, PlayerAccountId = player,
                EntryType = LedgerEntryTypeNames.TopUp, AccountType = LedgerAccountTypeNames.Wallet,
                AmountMinorUnits = walletMinorUnits, CurrencyCode = "TJS", CreatedAtUtc = Now
            });
        }

        db.PackageDefinitions.Add(new PackageDefinitionEntity
        {
            PackageDefinitionId = packageDefinitionId, OrganizationId = org, BranchId = branch,
            Name = "НОЧНОЙ 5Ч", CurrencyCode = "TJS", PriceMinorUnits = packagePriceMinorUnits,
            IncludedSeconds = 18_000, BonusSeconds = 1_800, ExpiresAfterDays = 30,
            IsActive = true, CreatedAtUtc = Now
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new Seeded(org, branch, player, phone, packageDefinitionId);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync("/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static string PurchaseUrl(Seeded seeded) =>
        $"/api/me/branches/{seeded.BranchId}/packages/{seeded.PackageDefinitionId}/purchase";

    // Клуб закрыт, смены нет — и это ровно тот момент, ради которого предоплата существует.
    [Fact]
    public async Task Purchase_GoesThroughWithNoOpenShift()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            PurchaseUrl(seeded),
            new PurchasePackageFromAppRequest("app-purchase-001"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var purchased = await response.Content.ReadFromJsonAsync<PlayerPackageDto>();
        Assert.Equal("НОЧНОЙ 5Ч", purchased!.Name);
        Assert.Equal(18_000, purchased.RemainingIncludedSeconds);
        Assert.Equal(1_800, purchased.RemainingBonusSeconds);
        Assert.Equal(40_000, purchased.PurchasedPrice.MinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var debit = await db.LedgerEntries.SingleAsync(entry =>
            entry.EntryType == LedgerEntryTypeNames.PackagePurchase &&
            entry.AccountType == LedgerAccountTypeNames.Wallet);
        Assert.Equal(-40_000, debit.AmountMinorUnits);
        Assert.Null(debit.ShiftId);
        Assert.Equal(SystemActorIds.PlayerSelfService, debit.CreatedByStaffUserId);
    }

    // Повтор с тем же ключом — обычное дело на телефоне: связь оборвалась, игрок нажал ещё раз.
    [Fact]
    public async Task Purchase_RepeatedWithTheSameKeyChargesOnce()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var first = await client.PostAsJsonAsync(PurchaseUrl(seeded), new PurchasePackageFromAppRequest("app-purchase-001"));
        var second = await client.PostAsJsonAsync(PurchaseUrl(seeded), new PurchasePackageFromAppRequest("app-purchase-001"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstPackage = await first.Content.ReadFromJsonAsync<PlayerPackageDto>();
        var secondPackage = await second.Content.ReadFromJsonAsync<PlayerPackageDto>();
        Assert.Equal(firstPackage!.PlayerPackageId, secondPackage!.PlayerPackageId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await db.PlayerPackages.ToListAsync());
    }

    [Fact]
    public async Task Purchase_RefusesWhenTheWalletIsShort()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234", walletMinorUnits: 30_000, packagePriceMinorUnits: 40_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            PurchaseUrl(seeded),
            new PurchasePackageFromAppRequest("app-purchase-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("insufficient_funds", body!.Error);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await db.PlayerPackages.ToListAsync());
    }

    [Fact]
    public async Task Purchase_RefusesAPackageFromAnotherClub()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            $"/api/me/branches/{stranger.BranchId}/packages/{stranger.PackageDefinitionId}/purchase",
            new PurchasePackageFromAppRequest("app-purchase-001"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Purchase_RequiresAnIdempotencyKey()
    {
        await using var factory = new PlatformApiFactory();
        var seeded = await SeedAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, seeded.OrgId, seeded.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            PurchaseUrl(seeded),
            new PurchasePackageFromAppRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MyPackages_ListsOnlyMyOwn()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedAsync(factory, "1234");
        var stranger = await SeedAsync(factory, "4321");

        using var strangerClient = factory.CreateClient();
        await AuthenticateAsync(strangerClient, stranger.OrgId, stranger.Phone, "4321");
        var strangerPurchase = await strangerClient.PostAsJsonAsync(
            $"/api/me/branches/{stranger.BranchId}/packages/{stranger.PackageDefinitionId}/purchase",
            new PurchasePackageFromAppRequest("stranger-purchase-001"));
        Assert.Equal(HttpStatusCode.OK, strangerPurchase.StatusCode);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine.OrgId, mine.Phone, "1234");
        await client.PostAsJsonAsync(PurchaseUrl(mine), new PurchasePackageFromAppRequest("my-purchase-001"));

        var packages = await client.GetFromJsonAsync<List<PlayerPackageDto>>("/api/me/packages");

        Assert.Single(packages!);
        Assert.Equal(mine.PlayerId, packages![0].PlayerAccountId);
        Assert.Equal(18_000, packages[0].RemainingIncludedSeconds);
    }

    private sealed record ErrorBody(string Error);
}
