using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

// Приёмка 30.09.2026: «Клиенты» были пусты всегда — экран спрашивал список без запроса, а сервер на
// запросе короче двух символов отдавал []. Итоги «На балансах / Долги» считались по первым
// пятидесяти загруженным, то есть у клуба с сотнями клиентов врали.
public sealed class PlayerDirectoryEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");
    private static readonly Guid OtherBranchId = Guid.Parse("0f97df06-14c0-469a-97d7-8f7850bc72b1");

    private static string Players(string query = "") =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/players{query}";

    [Fact]
    public async Task Directory_WithoutQuery_ListsClientsByNameInPages()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var firstPage = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?limit=51&includeInactive=true"));
        var secondPage = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?limit=51&offset=50&includeInactive=true"));

        // Всего 60 активных + 1 неактивный + 1 должник без баланса = 62 у этого клуба.
        Assert.Equal(51, firstPage!.Count);
        Assert.Equal(12, secondPage!.Count);
        Assert.Equal("Client 01", firstPage[0].DisplayName);
        Assert.DoesNotContain(secondPage, row => firstPage.Take(50).Any(seen => seen.PlayerAccountId == row.PlayerAccountId));
        Assert.DoesNotContain(firstPage.Concat(secondPage), row => row.DisplayName == "Other Branch Player");
    }

    [Fact]
    public async Task Directory_WithoutIncludeInactive_SkipsInactiveClients()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var rows = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?limit=100"));

        Assert.All(rows!, row => Assert.True(row.IsActive));
    }

    [Fact]
    public async Task Directory_OneLetterQuery_StillSearches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var rows = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?query=Z&limit=20"));

        Assert.Equal("Zed Debtor", Assert.Single(rows!).DisplayName);
    }

    [Fact]
    public async Task Directory_DebtSegment_ListsOnlyDebtors()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var rows = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?segment=debt&limit=50"));

        var debtor = Assert.Single(rows!);
        Assert.Equal("Zed Debtor", debtor.DisplayName);
        Assert.Equal(700, debtor.DebtBalanceMinorUnits);
    }

    [Fact]
    public async Task Directory_InactiveSegment_ListsOnlyInactiveWithoutIncludeInactive()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var rows = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(Players("?segment=inactive&limit=50"));

        Assert.Equal("Yan Inactive", Assert.Single(rows!).DisplayName);
    }

    [Fact]
    public async Task Directory_UnknownSegment_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        using var response = await client.GetAsync(Players("?segment=vip"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Summary_CountsAndSumsEveryClientNotJustOnePage()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var summary = await client.GetFromJsonAsync<PlayersSummaryDto>(Players("/summary"));

        Assert.Equal(62, summary!.TotalCount);
        Assert.Equal(1, summary.DebtorCount);
        Assert.Equal(1, summary.InactiveCount);
        // 60 клиентов по 100 + неактивный с 250 на кошельке; чужой филиал и отрицательный остаток не в счёт.
        Assert.Equal(60 * 100 + 250, summary.WalletTotalMinorUnits);
        Assert.Equal(700, summary.DebtTotalMinorUnits);
    }

    [Fact]
    public async Task Summary_WithQuery_CoversEveryMatchingClient()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedClubAsync(factory);

        var summary = await client.GetFromJsonAsync<PlayersSummaryDto>(Players("/summary?query=Zed"));

        Assert.Equal(1, summary!.TotalCount);
        Assert.Equal(1, summary.DebtorCount);
        Assert.Equal(0, summary.WalletTotalMinorUnits);
        Assert.Equal(700, summary.DebtTotalMinorUnits);
    }

    [Fact]
    public async Task Summary_WithTechnician_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        using var response = await client.GetAsync(Players("/summary"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedClubAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        for (var i = 1; i <= 60; i++)
        {
            var id = Guid.NewGuid();
            db.PlayerAccounts.Add(Player(id, $"Client {i:00}", TestIds.BranchId, isActive: true, phone: $"+992 90 000 {i:00} 00"));
            db.LedgerEntries.Add(Entry(id, LedgerAccountTypeNames.Wallet, 100, TestIds.BranchId));
        }

        var inactiveId = Guid.NewGuid();
        db.PlayerAccounts.Add(Player(inactiveId, "Yan Inactive", TestIds.BranchId, isActive: false, phone: null));
        db.LedgerEntries.Add(Entry(inactiveId, LedgerAccountTypeNames.Wallet, 250, TestIds.BranchId));

        var debtorId = Guid.NewGuid();
        db.PlayerAccounts.Add(Player(debtorId, "Zed Debtor", TestIds.BranchId, isActive: true, phone: null));
        db.LedgerEntries.Add(Entry(debtorId, LedgerAccountTypeNames.Debt, 700, TestIds.BranchId));
        // Минус на кошельке (после списаний) итог «На балансах» не уменьшает.
        db.LedgerEntries.Add(Entry(debtorId, LedgerAccountTypeNames.Wallet, -40, TestIds.BranchId));

        db.Branches.Add(new BranchEntity { BranchId = OtherBranchId, OrganizationId = TestIds.OrganizationId, Name = "Other Branch", CreatedAtUtc = Now });
        var otherId = Guid.NewGuid();
        db.PlayerAccounts.Add(Player(otherId, "Other Branch Player", OtherBranchId, isActive: true, phone: null));
        db.LedgerEntries.Add(Entry(otherId, LedgerAccountTypeNames.Wallet, 9999, OtherBranchId));

        await db.SaveChangesAsync();
    }

    private static PlayerAccountEntity Player(Guid id, string name, Guid branchId, bool isActive, string? phone) => new()
    {
        PlayerAccountId = id,
        OrganizationId = TestIds.OrganizationId,
        HomeBranchId = branchId,
        DisplayName = name,
        PhoneNumber = phone,
        IsActive = isActive,
        CreatedAtUtc = Now
    };

    private static LedgerEntryEntity Entry(Guid playerId, string accountType, long amount, Guid branchId) => new()
    {
        LedgerEntryId = Guid.NewGuid(),
        OrganizationId = TestIds.OrganizationId,
        BranchId = branchId,
        PlayerAccountId = playerId,
        EntryType = accountType == LedgerAccountTypeNames.Wallet ? LedgerEntryTypeNames.TopUp : LedgerEntryTypeNames.PostpaidDebt,
        AccountType = accountType,
        AmountMinorUnits = amount,
        QuantitySeconds = 0,
        CurrencyCode = "TJS",
        Description = "directory seed",
        Reason = "test",
        CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
        CreatedAtUtc = Now
    };
}
