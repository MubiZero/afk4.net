using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfPackageServiceTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerPackageId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid SessionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T11:00:00Z");

    [Fact]
    public async Task CreatePackageDefinitionAsync_CreatesActiveBranchPackage()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = CreatePackageRequest(idempotencyKey: "package-create-001");

        var first = await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.PackageDefinitionId, second.Response.PackageDefinitionId);

        var package = await db.PackageDefinitions.SingleAsync();
        Assert.Equal(first.Response.PackageDefinitionId, package.PackageDefinitionId);
        Assert.Equal(TestIds.OrganizationId, package.OrganizationId);
        Assert.Equal(TestIds.BranchId, package.BranchId);
        Assert.Equal("NIGHT 5H", package.Name);
        Assert.Equal("TJS", package.CurrencyCode);
        Assert.Equal(4000, package.PriceMinorUnits);
        Assert.Equal(18000, package.IncludedSeconds);
        Assert.Equal(1800, package.BonusSeconds);
        Assert.Equal(30, package.ExpiresAfterDays);
        Assert.True(package.IsActive);
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task CreatePackageDefinitionAsync_DuplicateNameWithDifferentIdempotencyKeyReturnsInvalidOrConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = CreatePackageRequest(idempotencyKey: "package-create-001");
        var duplicateRequest = request with
        {
            Name = "night 5h",
            IdempotencyKey = "package-create-002"
        };

        await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var duplicate = await service.CreatePackageDefinitionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            duplicateRequest,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Single(await db.PackageDefinitions.ToListAsync());
    }

    [Fact]
    public async Task CreatePackageDefinitionAsync_DuplicateRaceRecoveryReturnsInvalidWhenCanonicalNameNowExists()
    {
        await using var db = CreateDbContext();
        await SeedPackageDefinitionAsync(db);
        var service = CreateService(db);
        var request = CreatePackageRequest(idempotencyKey: "package-create-002");

        var recoveryMethod = typeof(EfPackageService).GetMethod(
            "RecoverPackageCreateRaceAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(recoveryMethod);
        var resultTask = Assert.IsAssignableFrom<Task<BillingCommandServiceResult<PackageDefinitionDto>?>>(
            recoveryMethod.Invoke(service, [TestIds.BranchId, request, CancellationToken.None]));
        var result = await resultTask;

        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.False(result.NotFound);
        Assert.Equal("Package name already exists.", result.Error);
        Assert.Single(await db.PackageDefinitions.ToListAsync());
    }

    [Fact]
    public async Task CreatePackageDefinitionAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = CreatePackageRequest(idempotencyKey: "package-create-001");
        var differentRequest = request with { Name = "Night 6h" };

        await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, differentRequest, CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.PackageDefinitions.ToListAsync());
    }

    [Theory]
    [InlineData("", 4000, 18000, 1800, 30, "TJS")]
    [InlineData("   ", 4000, 18000, 1800, 30, "TJS")]
    [InlineData("Night 5h", 0, 18000, 1800, 30, "TJS")]
    [InlineData("Night 5h", 4000, 0, 1800, 30, "TJS")]
    [InlineData("Night 5h", 4000, 18000, -1, 30, "TJS")]
    [InlineData("Night 5h", 4000, 18000, 1800, 0, "TJS")]
    [InlineData("Night 5h", 4000, 18000, 1800, 30, null)]
    [InlineData("Night 5h", 4000, 18000, 1800, 30, "USDT")]
    [InlineData("Night 5h", 4000, 18000, 1800, 30, "12$")]
    public async Task CreatePackageDefinitionAsync_RejectsInvalidInputs(
        string name,
        long priceMinorUnits,
        int includedSeconds,
        int bonusSeconds,
        int expiresAfterDays,
        string? currencyCode)
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new CreatePackageDefinitionRequest(
            TestIds.OrganizationId,
            name,
            new MoneyDto(currencyCode!, priceMinorUnits),
            includedSeconds,
            bonusSeconds,
            expiresAfterDays,
            "package-create-001");

        var result = await service.CreatePackageDefinitionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.PackageDefinitions.ToListAsync());
    }

    [Fact]
    public async Task PurchasePackageAsync_DebitsWalletCreatesPlayerPackageAndGrantsPackageAndBonusTime()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var definition = await SeedPackageDefinitionAsync(db, priceMinorUnits: 4000, includedSeconds: 18000, bonusSeconds: 1800);
        var service = CreateService(db);
        var request = new PurchasePackageRequest(TestIds.OrganizationId, definition.PackageDefinitionId, "package-purchase-001");

        var first = await service.PurchasePackageAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.PurchasePackageAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.PlayerPackageId, second.Response.PlayerPackageId);

        var playerPackage = await db.PlayerPackages.SingleAsync();
        Assert.Equal(first.Response.PlayerPackageId, playerPackage.PlayerPackageId);
        Assert.Equal(definition.PackageDefinitionId, playerPackage.PackageDefinitionId);
        Assert.Equal("NIGHT 5H", playerPackage.Name);
        Assert.Equal("TJS", playerPackage.CurrencyCode);
        Assert.Equal(4000, playerPackage.PurchasedPriceMinorUnits);
        Assert.Equal(18000, playerPackage.IncludedSeconds);
        Assert.Equal(1800, playerPackage.BonusSeconds);
        Assert.Equal(Now.AddDays(30), playerPackage.ExpiresAtUtc);

        var packageEntries = await db.LedgerEntries
            .Where(entry => entry.EntryType != LedgerEntryTypeNames.TopUp)
            .OrderBy(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.LedgerEntryId)
            .ToListAsync();
        Assert.Equal(3, packageEntries.Count);
        Assert.Collection(
            packageEntries,
            debit =>
            {
                Assert.Equal(LedgerEntryTypeNames.PackagePurchase, debit.EntryType);
                Assert.Equal(LedgerAccountTypeNames.Wallet, debit.AccountType);
                Assert.Equal(-4000, debit.AmountMinorUnits);
                Assert.Equal(0, debit.QuantitySeconds);
            },
            packageTime =>
            {
                Assert.Equal(LedgerEntryTypeNames.PackagePurchase, packageTime.EntryType);
                Assert.Equal(LedgerAccountTypeNames.PackageTime, packageTime.AccountType);
                Assert.Equal(0, packageTime.AmountMinorUnits);
                Assert.Equal(18000, packageTime.QuantitySeconds);
            },
            bonus =>
            {
                Assert.Equal(LedgerEntryTypeNames.BonusGrant, bonus.EntryType);
                Assert.Equal(LedgerAccountTypeNames.BonusTime, bonus.AccountType);
                Assert.Equal(0, bonus.AmountMinorUnits);
                Assert.Equal(1800, bonus.QuantitySeconds);
            });
        Assert.All(packageEntries, entry => Assert.Equal(playerPackage.PlayerPackageId, entry.PlayerPackageId));
        Assert.Equal(4, await db.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task PurchasePackageAsync_RejectsInsufficientWalletBalance()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 3000);
        var definition = await SeedPackageDefinitionAsync(db, priceMinorUnits: 4000);
        var service = CreateService(db);
        var request = new PurchasePackageRequest(TestIds.OrganizationId, definition.PackageDefinitionId, "package-purchase-001");

        var result = await service.PurchasePackageAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.PlayerPackages.ToListAsync());
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PackagePurchase).ToListAsync());
    }

    [Fact]
    public async Task PurchasePackageAsync_RejectsMismatchedCurrency()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000, currencyCode: "TJS");
        var definition = await SeedPackageDefinitionAsync(db, priceMinorUnits: 4000, currencyCode: "USD");
        var service = CreateService(db);
        var request = new PurchasePackageRequest(TestIds.OrganizationId, definition.PackageDefinitionId, "package-purchase-001");

        var result = await service.PurchasePackageAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.PlayerPackages.ToListAsync());
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PackagePurchase).ToListAsync());
    }

    [Fact]
    public async Task ConsumePackageTimeAsync_ConsumesBonusSecondsFirstThenIncludedPackageSeconds()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 3600);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.BonusGrant, LedgerAccountTypeNames.BonusTime, 600);
        var service = CreateService(db);

        var result = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            PlayerPackageId,
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            durationSeconds: 900,
            idempotencyKey: "package-consume-001",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(2, result.Response.Count);
        Assert.Collection(
            result.Response,
            bonus =>
            {
                Assert.Equal(LedgerEntryTypeNames.BonusConsumption, bonus.EntryType);
                Assert.Equal(LedgerAccountTypeNames.BonusTime, bonus.AccountType);
                Assert.Equal(-600, bonus.QuantitySeconds);
                Assert.Equal(0, bonus.Amount.MinorUnits);
                Assert.Equal(SessionId, bonus.SessionId);
                Assert.Equal(PlayerPackageId, bonus.PlayerPackageId);
            },
            package =>
            {
                Assert.Equal(LedgerEntryTypeNames.PackageConsumption, package.EntryType);
                Assert.Equal(LedgerAccountTypeNames.PackageTime, package.AccountType);
                Assert.Equal(-300, package.QuantitySeconds);
                Assert.Equal(0, package.Amount.MinorUnits);
                Assert.Equal(SessionId, package.SessionId);
                Assert.Equal(PlayerPackageId, package.PlayerPackageId);
            });

        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(db, PlayerPackageId, CancellationToken.None);
        Assert.Equal(3300, remaining.IncludedSeconds);
        Assert.Equal(0, remaining.BonusSeconds);
    }

    [Fact]
    public async Task ConsumePackageTimeAsync_RejectsExpiredPackages()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db, expiresAtUtc: Now.AddSeconds(-1));
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 3600);
        var service = CreateService(db);

        var result = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            PlayerPackageId,
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            900,
            "package-consume-001",
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PackageConsumption).ToListAsync());
    }

    [Fact]
    public async Task ConsumePackageTimeAsync_RejectsInsufficientRemainingSeconds()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 300);
        var service = CreateService(db);

        var result = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            PlayerPackageId,
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            900,
            "package-consume-001",
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.EntryType.EndsWith("consumption", StringComparison.Ordinal)).ToListAsync());
    }

    [Fact]
    public async Task UnknownPlayerOrPackageCommands_ReturnNotFound()
    {
        await using var db = CreateDbContext();
        await SeedPackageDefinitionAsync(db);
        var definition = await db.PackageDefinitions.SingleAsync();
        await SeedPlayerAsync(db);
        var service = CreateService(db);

        var purchase = await service.PurchasePackageAsync(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            TestIds.BranchId,
            ActorStaffUserId,
            new PurchasePackageRequest(TestIds.OrganizationId, definition.PackageDefinitionId, "package-purchase-001"),
            CancellationToken.None);
        var consume = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            900,
            "package-consume-001",
            CancellationToken.None);

        Assert.True(purchase.NotFound);
        Assert.True(consume.NotFound);
        Assert.Empty(await db.PlayerPackages.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task ConsumePackageTimeAsync_RepeatedWithSameIdempotencyKeyReturnsOriginalEntries()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 3600);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.BonusGrant, LedgerAccountTypeNames.BonusTime, 600);
        var service = CreateService(db);

        var first = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            PlayerPackageId,
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            900,
            "package-consume-001",
            CancellationToken.None);
        var second = await service.ConsumePackageTimeAsync(
            PlayerAccountId,
            PlayerPackageId,
            TestIds.BranchId,
            SessionId,
            ActorStaffUserId,
            900,
            "package-consume-001",
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.Select(entry => entry.LedgerEntryId), second.Response.Select(entry => entry.LedgerEntryId));
        Assert.Equal(2, await db.LedgerEntries.Where(entry => entry.EntryType.EndsWith("consumption", StringComparison.Ordinal)).CountAsync());
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfPackageService CreateService(PlatformDbContext db)
    {
        return new EfPackageService(db, new FixedTimeProvider(Now));
    }

    private static CreatePackageDefinitionRequest CreatePackageRequest(string idempotencyKey)
    {
        return new CreatePackageDefinitionRequest(
            TestIds.OrganizationId,
            "Night 5h",
            new MoneyDto("TJS", 4000),
            18000,
            1800,
            30,
            idempotencyKey);
    }

    private static async Task SeedPlayerAsync(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });

        await db.SaveChangesAsync();
    }

    private static async Task<PackageDefinitionEntity> SeedPackageDefinitionAsync(
        PlatformDbContext db,
        long priceMinorUnits = 4000,
        int includedSeconds = 18000,
        int bonusSeconds = 1800,
        string currencyCode = "TJS")
    {
        var package = new PackageDefinitionEntity
        {
            PackageDefinitionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "NIGHT 5H",
            CurrencyCode = currencyCode,
            PriceMinorUnits = priceMinorUnits,
            IncludedSeconds = includedSeconds,
            BonusSeconds = bonusSeconds,
            ExpiresAfterDays = 30,
            IsActive = true,
            CreatedAtUtc = Now
        };

        db.PackageDefinitions.Add(package);
        await db.SaveChangesAsync();

        return package;
    }

    private static async Task SeedWalletTopUpAsync(PlatformDbContext db, long amountMinorUnits, string currencyCode = "TJS")
    {
        db.LedgerEntries.Add(CreateLedgerEntry(
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            amountMinorUnits,
            quantitySeconds: 0,
            playerPackageId: null,
            currencyCode));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPlayerPackageAsync(PlatformDbContext db, DateTimeOffset? expiresAtUtc = null)
    {
        db.PlayerPackages.Add(new PlayerPackageEntity
        {
            PlayerPackageId = PlayerPackageId,
            PackageDefinitionId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            Name = "NIGHT 5H",
            CurrencyCode = "TJS",
            PurchasedPriceMinorUnits = 4000,
            IncludedSeconds = 3600,
            BonusSeconds = 600,
            PurchasedAtUtc = Now,
            ExpiresAtUtc = expiresAtUtc ?? Now.AddDays(30)
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackageGrantAsync(
        PlatformDbContext db,
        string entryType,
        string accountType,
        int quantitySeconds)
    {
        db.LedgerEntries.Add(CreateLedgerEntry(
            entryType,
            accountType,
            amountMinorUnits: 0,
            quantitySeconds,
            PlayerPackageId,
            "TJS"));
        await db.SaveChangesAsync();
    }

    private static LedgerEntryEntity CreateLedgerEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        Guid? playerPackageId,
        string currencyCode)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            SessionId = null,
            PlayerPackageId = playerPackageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = entryType,
            Reason = "test seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
