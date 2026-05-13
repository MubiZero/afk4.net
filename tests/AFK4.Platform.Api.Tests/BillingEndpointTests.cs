using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class BillingEndpointTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid ActorStaffUserId = TestIds.TechnicianStaffUserId;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task CreatePlayer_WithoutBearer_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players",
            new CreatePlayerAccountRequest(TestIds.OrganizationId, "Player One", "+992000000001", "player-create-001"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePlayer_WithCashier_CreatesPlayerAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players",
            new CreatePlayerAccountRequest(TestIds.OrganizationId, "Player One", "+992000000001", "player-create-001"));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.BranchId, body.HomeBranchId);
        Assert.Equal("Player One", body.DisplayName);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await dbContext.PlayerAccounts.AnyAsync(player => player.PlayerAccountId == body.PlayerAccountId));
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreatePlayerAccount, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(body.PlayerAccountId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task TopUpWallet_WithCashier_AppendsTopUp()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/wallet/top-ups",
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-001"));
        var body = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(5000, body.WalletBalance.MinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = await dbContext.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryTypeNames.TopUp, entry.EntryType);
        Assert.Equal(5000, entry.AmountMinorUnits);
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.TopUpWallet, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task ManualCorrection_WithCashier_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/manual-corrections",
            new ManualLedgerCorrectionRequest(
                TestIds.OrganizationId,
                LedgerAccountTypeNames.Wallet,
                new MoneyDto("TJS", -300),
                0,
                "manager correction for dispute",
                "correction-001"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ManualLedgerCorrection, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task ManualCorrection_WithShiftSupervisor_Succeeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.ShiftSupervisor);
        await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/ledger/manual-corrections",
            new ManualLedgerCorrectionRequest(
                TestIds.OrganizationId,
                LedgerAccountTypeNames.Wallet,
                new MoneyDto("TJS", -300),
                0,
                "manager correction for dispute",
                "correction-001"));
        var body = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(-300, body.WalletBalance.MinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = await dbContext.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntryTypeNames.ManualCorrection, entry.EntryType);
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ManualLedgerCorrection, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task WalletSummary_WithAccountant_ReturnsDerivedBalances()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);
        await SeedPlayerAsync(factory);
        await SeedLedgerEntryAsync(factory, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000);
        await SeedLedgerEntryAsync(factory, LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 1200);

        var response = await client.GetAsync($"/api/players/{PlayerAccountId:D}/wallet-summary");
        var body = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(5000, body.WalletBalance.MinorUnits);
        Assert.Equal(1200, body.DebtBalance.MinorUnits);
        Assert.Equal(2, body.RecentEntries.Count);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
    }

    [Fact]
    public async Task CreateTariff_WithBranchManager_CreatesTariff()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs",
            new CreateTariffRequest(TestIds.OrganizationId, "Standard", "tariff-create-001"));
        var body = await response.Content.ReadFromJsonAsync<TariffDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Standard", body.Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreateTariff, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task CreateTariffVersion_WithBranchManager_CreatesVersion()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var tariff = await SeedTariffAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/{tariff.TariffId:D}/versions",
            new CreateTariffVersionRequest(
                TestIds.OrganizationId,
                tariff.TariffId,
                "TJS",
                50,
                30,
                15,
                Now,
                "tariff-version-001"));
        var body = await response.Content.ReadFromJsonAsync<TariffVersionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(tariff.TariffId, body.TariffId);
        Assert.Equal(1, body.VersionNumber);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreateTariffVersion, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task CalculateTariff_WithAuthorizedStaff_ReturnsCalculation()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var tariff = await SeedTariffAsync(factory);
        var version = await SeedTariffVersionAsync(factory, tariff.TariffId);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/calculate",
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 76));
        var body = await response.Content.ReadFromJsonAsync<TariffCalculationResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(90, body.BillableMinutes);
        Assert.Equal(4500, body.Amount.MinorUnits);
    }

    [Fact]
    public async Task CreatePackage_WithBranchManager_CreatesPackageDefinition()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/packages",
            CreatePackageRequest("package-create-001"));
        var body = await response.Content.ReadFromJsonAsync<PackageDefinitionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("NIGHT 5H", body.Name);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreatePackageDefinition, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task PurchasePackage_WithCashier_PurchasesPackage()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        await SeedLedgerEntryAsync(factory, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000);
        var package = await SeedPackageDefinitionAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/packages/purchases",
            new PurchasePackageRequest(TestIds.OrganizationId, package.PackageDefinitionId, "package-purchase-001"));
        var body = await response.Content.ReadFromJsonAsync<PlayerPackageDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(package.PackageDefinitionId, body.PackageDefinitionId);
        Assert.Equal(18000, body.RemainingIncludedSeconds);
        Assert.Equal(1800, body.RemainingBonusSeconds);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(4, await dbContext.LedgerEntries.CountAsync());
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.PurchasePackage, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyWithDifferentEndpointRequest_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        var request = new TopUpWalletRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 5000),
            "front desk cash top-up",
            "topup-001");

        var first = await client.PostAsJsonAsync($"/api/players/{PlayerAccountId:D}/wallet/top-ups", request);
        var conflict = await client.PostAsJsonAsync(
            $"/api/players/{PlayerAccountId:D}/wallet/top-ups",
            request with { Amount = new MoneyDto("TJS", 7000) });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private static async Task SeedPlayerAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLedgerEntryAsync(
        PlatformApiFactory factory,
        string entryType,
        string accountType,
        long amountMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            SessionId = null,
            PlayerPackageId = null,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<TariffEntity> SeedTariffAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };
        dbContext.Tariffs.Add(tariff);
        await dbContext.SaveChangesAsync();

        return tariff;
    }

    private static async Task<TariffVersionEntity> SeedTariffVersionAsync(PlatformApiFactory factory, Guid tariffId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var version = new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = Now.AddMinutes(-1),
            RetiredAtUtc = null,
            CreatedAtUtc = Now
        };
        dbContext.TariffVersions.Add(version);
        await dbContext.SaveChangesAsync();

        return version;
    }

    private static async Task<PackageDefinitionEntity> SeedPackageDefinitionAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var package = new PackageDefinitionEntity
        {
            PackageDefinitionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "NIGHT 5H",
            CurrencyCode = "TJS",
            PriceMinorUnits = 4000,
            IncludedSeconds = 18000,
            BonusSeconds = 1800,
            ExpiresAfterDays = 30,
            IsActive = true,
            CreatedAtUtc = Now
        };
        dbContext.PackageDefinitions.Add(package);
        await dbContext.SaveChangesAsync();

        return package;
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
}
