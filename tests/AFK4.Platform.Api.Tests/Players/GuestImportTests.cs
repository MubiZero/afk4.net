using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>Перенос гостей из прежней программы: карточки и начальные остатки, без выручки и без двойного переноса.</summary>
public sealed class GuestImportTests
{
    private static GuestImportRequest Request(DevicePlayerFixture fixture, bool dryRun, string key, params GuestImportRowDto[] rows) =>
        new(fixture.Device.OrganizationId, "TJS", "SmartShell", rows, dryRun, key);

    [Fact]
    public async Task ADryRun_WritesNothing_AndTheImport_CreatesGuestsWithOpeningBalances()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var rows = new[]
        {
            new GuestImportRowDto("93 737 00 70", "Фарход", 5000, 1000),
            new GuestImportRowDto(fixture.Phone, "Уже в клубе", 2000, 0),
            new GuestImportRowDto("123", "Кривой номер", 100, 0),
            new GuestImportRowDto("+992 937 370 070", "Фарход ещё раз", 100, 0),
            new GuestImportRowDto("+992900000111", "", 100, 0)
        };

        var preview = await ImportAsync(fixture, Request(fixture, dryRun: true, "import-1", rows));
        Assert.False(preview.Committed);
        Assert.Equal(1, preview.Created);
        Assert.Equal(1, preview.Matched);
        Assert.Equal(3, preview.Skipped);
        Assert.Equal(
            [GuestImportIssueNames.InvalidPhone, GuestImportIssueNames.DuplicateInFile, GuestImportIssueNames.MissingName],
            preview.Issues.Select(issue => issue.Code));
        Assert.Equal(0, await OpeningEntriesAsync(fixture));

        var done = await ImportAsync(fixture, Request(fixture, dryRun: false, "import-1", rows));
        Assert.True(done.Committed);
        Assert.Equal(new MoneyDto("TJS", 7000), done.BalanceTotal);
        Assert.Equal(new MoneyDto("TJS", 1000), done.BonusTotal);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var farhod = await db.PlayerAccounts.SingleAsync(player => player.DisplayName == "Фарход");
        Assert.Equal("+992937370070", farhod.PhoneNumber);
        var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(db, farhod.PlayerAccountId, CancellationToken.None);
        Assert.Equal(6000, wallet!.WalletBalance.MinorUnits);
        var existing = await LedgerBalanceProjector.GetWalletSummaryAsync(db, fixture.PlayerAccountId, CancellationToken.None);
        Assert.Equal(2000, existing!.WalletBalance.MinorUnits);
        // Начальный остаток — не выручка и не наличные смены.
        var entries = await db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.OpeningBalance).ToListAsync();
        Assert.All(entries, entry => Assert.Null(entry.ShiftId));
        Assert.Equal(0, BranchRevenue.Gameplay(entries.Select(entry => (Kind: entry.EntryType, entry.AmountMinorUnits))));
    }

    [Fact]
    public async Task ASecondImport_DoesNotDoubleAnyonesMoney()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        var row = new GuestImportRowDto(fixture.Phone, "Гость", 3000, 0);

        await ImportAsync(fixture, Request(fixture, dryRun: false, "import-1", row));
        // Тот же ключ — тот же ответ; другой ключ — «уже перенесён».
        var replay = await ImportAsync(fixture, Request(fixture, dryRun: false, "import-1", row));
        Assert.Equal(1, replay.Matched);
        var again = await ImportAsync(fixture, Request(fixture, dryRun: false, "import-2", row));
        Assert.Equal(GuestImportIssueNames.AlreadyImported, Assert.Single(again.Issues).Code);

        Assert.Equal(1, await OpeningEntriesAsync(fixture));
    }

    [Theory]
    [InlineData("937370070", "992937370070")]
    [InlineData("+992 (93) 737-00-70", "992937370070")]
    [InlineData("8 800", null)]
    public void ALocalTajikNumber_GetsTheCountryCode(string raw, string? expected) =>
        Assert.Equal(expected, GuestImport.NormalizePhone(raw));

    private static async Task<GuestImportResultDto> ImportAsync(DevicePlayerFixture fixture, GuestImportRequest request)
    {
        var response = await fixture.Client.PostAsJsonAsync(
            $"/api/organizations/{fixture.Device.OrganizationId:D}/branches/{fixture.Device.BranchId:D}/players/import", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuestImportResultDto>())!;
    }

    private static async Task<int> OpeningEntriesAsync(DevicePlayerFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.OpeningBalance);
    }
}
