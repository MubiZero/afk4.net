using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OperatorReferenceDataEndpointTests
{
    private static readonly Guid AlexPlayerId = Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2");
    private static readonly Guid OtherPlayerId = Guid.Parse("75b9b565-eb5c-4ff5-890c-85f3e12a0fc3");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public async Task SearchPlayers_WithCashier_ReturnsMatchingActivePlayersFromBranch()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayersAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/players?query=Alex&limit=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlayerSearchResultDto>>();

        Assert.NotNull(body);
        var result = Assert.Single(body);
        Assert.Equal(AlexPlayerId, result.PlayerAccountId);
        Assert.Equal("Alex Player", result.DisplayName);
        Assert.Equal(12000, result.WalletBalanceMinorUnits);
        Assert.Equal(2500, result.DebtBalanceMinorUnits);
        Assert.Equal(1, result.ActivePackageCount);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task TariffOptions_WithCashier_ReturnsActiveTariffVersions()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var activeVersion = await SeedTariffsAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/tariffs/options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<TariffOptionDto>>();

        Assert.NotNull(body);
        var option = Assert.Single(body);
        Assert.Equal(activeVersion.TariffVersionId, option.TariffVersionId);
        Assert.Equal("Standard", option.Name);
        Assert.Equal(activeVersion.TariffVersionId.ToString("D"), option.TariffRuleVersionId);
        Assert.Equal(2, option.VersionNumber);
        Assert.Equal(50, option.PricePerMinuteMinorUnits);
    }

    [Fact]
    public async Task PackageOptions_WithCashier_ReturnsActivePackageDefinitions()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var activePackage = await SeedPackagesAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/packages/options");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<PackageOptionDto>>();

        Assert.NotNull(body);
        var option = Assert.Single(body);
        Assert.Equal(activePackage.PackageDefinitionId, option.PackageDefinitionId);
        Assert.Equal("Night 5h", option.Name);
        Assert.Equal("TJS", option.CurrencyCode);
        Assert.Equal(4000, option.PriceMinorUnits);
        Assert.Equal(18000, option.IncludedSeconds);
        Assert.Equal(1800, option.BonusSeconds);
    }

    [Fact]
    public async Task ReferenceEndpoints_WithTechnician_ReturnForbiddenAndWriteDeniedAuditRows()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var endpointCases = new[]
        {
            new EndpointCase($"/api/branches/{TestIds.BranchId:D}/players?query=Alex&limit=20", AuditActionNames.ViewPlayers),
            new EndpointCase($"/api/branches/{TestIds.BranchId:D}/tariffs/options", AuditActionNames.ViewTariffs),
            new EndpointCase($"/api/branches/{TestIds.BranchId:D}/packages/options", AuditActionNames.ViewPackages)
        };

        foreach (var endpoint in endpointCases)
        {
            using var response = await client.GetAsync(endpoint.Path);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audits = await dbContext.AuditRecords
            .OrderBy(audit => audit.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(endpointCases.Length, audits.Count);
        foreach (var endpoint in endpointCases)
        {
            Assert.Contains(audits, audit => audit.Action == endpoint.ExpectedAuditAction && audit.Outcome == AuditOutcome.Denied);
        }
    }

    private static async Task SeedPlayersAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.PlayerAccounts.AddRange(
            new PlayerAccountEntity
            {
                PlayerAccountId = AlexPlayerId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Alex Player",
                PhoneNumber = "+992000000001",
                IsActive = true,
                CreatedAtUtc = Now
            },
            new PlayerAccountEntity
            {
                PlayerAccountId = OtherPlayerId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Inactive Alex",
                PhoneNumber = "+992000000002",
                IsActive = false,
                CreatedAtUtc = Now
            });
        dbContext.LedgerEntries.AddRange(
            CreateLedgerEntry(AlexPlayerId, LedgerAccountTypeNames.Wallet, 12000),
            CreateLedgerEntry(AlexPlayerId, LedgerAccountTypeNames.Debt, 2500));
        dbContext.PlayerPackages.Add(new PlayerPackageEntity
        {
            PlayerPackageId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
            PackageDefinitionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = AlexPlayerId,
            Name = "Night 5h",
            CurrencyCode = "TJS",
            PurchasedPriceMinorUnits = 4000,
            IncludedSeconds = 18000,
            BonusSeconds = 1800,
            PurchasedAtUtc = Now,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<TariffVersionEntity> SeedTariffsAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var activeTariffId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var inactiveTariffId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var activeVersion = new TariffVersionEntity
        {
            TariffVersionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            TariffId = activeTariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 2,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            RetiredAtUtc = null,
            CreatedAtUtc = Now
        };
        dbContext.Tariffs.AddRange(
            new TariffEntity
            {
                TariffId = activeTariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Standard",
                IsActive = true,
                CreatedAtUtc = Now
            },
            new TariffEntity
            {
                TariffId = inactiveTariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Archived",
                IsActive = false,
                CreatedAtUtc = Now
            });
        dbContext.TariffVersions.AddRange(
            new TariffVersionEntity
            {
                TariffVersionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
                TariffId = activeTariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                VersionNumber = 1,
                CurrencyCode = "TJS",
                PricePerMinuteMinorUnits = 40,
                MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15,
                EffectiveFromUtc = DateTimeOffset.UtcNow.AddHours(-2),
                RetiredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
                CreatedAtUtc = Now.AddMinutes(-10)
            },
            activeVersion,
            new TariffVersionEntity
            {
                TariffVersionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
                TariffId = inactiveTariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                VersionNumber = 1,
                CurrencyCode = "TJS",
                PricePerMinuteMinorUnits = 25,
                MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15,
                EffectiveFromUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
                RetiredAtUtc = null,
                CreatedAtUtc = Now
            });
        await dbContext.SaveChangesAsync();

        return activeVersion;
    }

    private static async Task<PackageDefinitionEntity> SeedPackagesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var activePackage = new PackageDefinitionEntity
        {
            PackageDefinitionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Night 5h",
            CurrencyCode = "TJS",
            PriceMinorUnits = 4000,
            IncludedSeconds = 18000,
            BonusSeconds = 1800,
            ExpiresAfterDays = 30,
            IsActive = true,
            CreatedAtUtc = Now
        };
        dbContext.PackageDefinitions.AddRange(
            activePackage,
            new PackageDefinitionEntity
            {
                PackageDefinitionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Archived package",
                CurrencyCode = "TJS",
                PriceMinorUnits = 1000,
                IncludedSeconds = 3600,
                BonusSeconds = 0,
                ExpiresAfterDays = 7,
                IsActive = false,
                CreatedAtUtc = Now
            });
        await dbContext.SaveChangesAsync();

        return activePackage;
    }

    private static LedgerEntryEntity CreateLedgerEntry(Guid playerAccountId, string accountType, long amountMinorUnits)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = playerAccountId,
            EntryType = accountType == LedgerAccountTypeNames.Wallet
                ? LedgerEntryTypeNames.TopUp
                : LedgerEntryTypeNames.PostpaidDebt,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = "reference seed",
            Reason = "test",
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            CreatedAtUtc = Now
        };
    }

    private sealed record EndpointCase(string Path, string ExpectedAuditAction);
}
