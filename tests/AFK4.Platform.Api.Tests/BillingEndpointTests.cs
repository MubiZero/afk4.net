using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class BillingEndpointTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid CrossBranchId = Guid.Parse("0f97df06-14c0-469a-97d7-8f7850bc72b0");
    private static readonly Guid CrossBranchPlayerAccountId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
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
    public async Task UpdatePlayer_WithCashier_UpdatesNameAndPhoneAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "Player Renamed", "+992000000099"));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Player Renamed", body.DisplayName);
        Assert.Equal("+992000000099", body.PhoneNumber);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == PlayerAccountId);
        Assert.Equal("Player Renamed", stored.DisplayName);
        var audit = await dbContext.AuditRecords.SingleAsync(a => a.Action == AuditActionNames.UpdatePlayerAccount);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(PlayerAccountId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task UpdatePlayer_BlankName_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePlayer_UnknownPlayer_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{Guid.NewGuid():D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "Ghost", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatePlayer_WithCashier_SetsInactiveAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == PlayerAccountId);
        Assert.False(stored.IsActive);
        var audit = await dbContext.AuditRecords.SingleAsync(a => a.Action == AuditActionNames.DeactivatePlayerAccount);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task ReactivatePlayer_WithCashier_SetsActiveAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        // деактивируем, затем реактивируем
        await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, true));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await dbContext.AuditRecords.AnyAsync(a => a.Action == AuditActionNames.ActivatePlayerAccount));
    }

    [Fact]
    public async Task TopUpWallet_WithCashier_AppendsTopUp()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        await SeedOpenShiftAsync(factory);

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
    public async Task TopUpWallet_WithTechnicianForUnknownPlayer_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var unknownPlayerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        var response = await client.PostAsJsonAsync(
            $"/api/players/{unknownPlayerId:D}/wallet/top-ups",
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-unknown-001"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.TopUpWallet, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(unknownPlayerId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task TopUpWallet_WithCashierForUnknownPlayer_ReturnsNotFoundAfterAuthorization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var unknownPlayerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        var response = await client.PostAsJsonAsync(
            $"/api/players/{unknownPlayerId:D}/wallet/top-ups",
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-unknown-001"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task TopUpWallet_WithCashierForCrossBranchPlayer_ReturnsNotFoundWithoutDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedCrossBranchPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{CrossBranchPlayerAccountId:D}/wallet/top-ups",
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-cross-branch-001"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
        Assert.Empty(await dbContext.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task TopUpWallet_WithTechnicianForCrossBranchPlayer_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedCrossBranchPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/players/{CrossBranchPlayerAccountId:D}/wallet/top-ups",
            new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "front desk cash top-up", "topup-cross-branch-001"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.TopUpWallet, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(CrossBranchPlayerAccountId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task WalletSummary_WithAccountantForUnknownPlayer_ReturnsNotFoundAfterAuthorization()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);
        var unknownPlayerId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        var response = await client.GetAsync($"/api/players/{unknownPlayerId:D}/wallet-summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WalletSummary_WithCashierForCrossBranchPlayer_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedCrossBranchPlayerAsync(factory);

        var response = await client.GetAsync($"/api/players/{CrossBranchPlayerAccountId:D}/wallet-summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        await SeedOpenShiftAsync(factory);

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
    public async Task UpdateTariff_WithBranchManager_UpdatesTariffAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var tariff = await SeedTariffAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/{tariff.TariffId:D}",
            new UpdateTariffRequest(TestIds.OrganizationId, "Standard Plus", false));
        var body = await response.Content.ReadFromJsonAsync<TariffDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Standard Plus", body.Name);
        Assert.False(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.UpdateTariff, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task UpdateTariffVersion_WithBranchManager_UpdatesVersionAndCanRetire()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var tariff = await SeedTariffAsync(factory);
        var version = await SeedTariffVersionAsync(factory, tariff.TariffId);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/{tariff.TariffId:D}/versions/{version.TariffVersionId:D}",
            new UpdateTariffVersionRequest(
                TestIds.OrganizationId,
                "TJS",
                75,
                20,
                10,
                Now,
                true));
        var updated = await updateResponse.Content.ReadFromJsonAsync<TariffVersionDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(75, updated.PricePerMinuteMinorUnits);
        Assert.Null(updated.RetiredAtUtc);

        var retireResponse = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/{tariff.TariffId:D}/versions/{version.TariffVersionId:D}",
            new UpdateTariffVersionRequest(
                TestIds.OrganizationId,
                "TJS",
                75,
                20,
                10,
                Now,
                false));
        var retired = await retireResponse.Content.ReadFromJsonAsync<TariffVersionDto>();

        Assert.Equal(HttpStatusCode.OK, retireResponse.StatusCode);
        Assert.NotNull(retired);
        Assert.NotNull(retired.RetiredAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, await dbContext.AuditRecords.CountAsync(audit => audit.Action == AuditActionNames.UpdateTariffVersion));
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
    public async Task CalculateTariff_WithNonPositiveDuration_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var tariff = await SeedTariffAsync(factory);
        var version = await SeedTariffVersionAsync(factory, tariff.TariffId);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/calculate",
            new CalculateTariffRequest(TestIds.OrganizationId, version.TariffVersionId, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CalculateTariff_WithUnknownVersion_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/tariffs/calculate",
            new CalculateTariffRequest(TestIds.OrganizationId, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), 30));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    public async Task UpdatePackage_WithBranchManager_UpdatesPackageDefinition()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var package = await SeedPackageDefinitionAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/packages/{package.PackageDefinitionId:D}",
            new UpdatePackageDefinitionRequest(
                TestIds.OrganizationId,
                "Night 6h",
                new MoneyDto("TJS", 4500),
                IncludedSeconds: 21600,
                BonusSeconds: 2400,
                ExpiresAfterDays: 45,
                IsActive: false));
        var body = await response.Content.ReadFromJsonAsync<PackageDefinitionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("NIGHT 6H", body.Name);
        Assert.False(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.UpdatePackageDefinition, audit.Action);
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
        await SeedOpenShiftAsync(factory);

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
    public async Task DuplicateIdempotencyKeyWithDifferentSameOperationRequest_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        await SeedOpenShiftAsync(factory);
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

    [Fact]
    public async Task SearchPlayers_ExcludesInactiveByDefault_IncludesWhenRequested()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        // деактивируем сид-игрока
        await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));

        var defaultSearch = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(
            $"/api/branches/{TestIds.BranchId:D}/players?query=Player");
        var inclusiveSearch = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(
            $"/api/branches/{TestIds.BranchId:D}/players?query=Player&includeInactive=true");

        Assert.NotNull(defaultSearch);
        Assert.DoesNotContain(defaultSearch!, p => p.PlayerAccountId == PlayerAccountId);
        Assert.NotNull(inclusiveSearch);
        Assert.Contains(inclusiveSearch!, p => p.PlayerAccountId == PlayerAccountId && !p.IsActive);
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

    private static async Task SeedCrossBranchPlayerAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = CrossBranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Other Branch",
            CreatedAtUtc = Now
        });
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = CrossBranchPlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = CrossBranchId,
            DisplayName = "Other Branch Player",
            PhoneNumber = "+992000000002",
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

    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
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
