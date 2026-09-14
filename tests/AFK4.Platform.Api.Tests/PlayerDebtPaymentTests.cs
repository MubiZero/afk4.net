using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Игрок гасит долг собственными деньгами с кошелька.
///
/// Раньше долг он только видел, а надпись отправляла его на стойку клуба — при том, что деньги
/// могли лежать на его же кошельке, и закрыть долг до следующего визита было нечем.
/// </summary>
public sealed class PlayerDebtPaymentTests
{
    private const string Pin = "1234";
    private const string Path = "/api/me/wallet/debt-payment";

    private static async Task SeedLedgerAsync(
        PlatformApiFactory factory,
        TopUpTestData.SeededPlayer player,
        string entryType,
        string accountType,
        long amountMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = player.OrgId,
            BranchId = player.BranchId,
            PlayerAccountId = player.PlayerId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test seed",
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<(HttpClient Client, TopUpTestData.SeededPlayer Player)> PlayerWithAsync(
        PlatformApiFactory factory,
        HttpClient client,
        long walletMinorUnits,
        long debtMinorUnits,
        long heldMinorUnits = 0)
    {
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        if (walletMinorUnits != 0)
            await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, walletMinorUnits);
        if (debtMinorUnits != 0)
            await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, debtMinorUnits);
        if (heldMinorUnits != 0)
            await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -heldMinorUnits);
        return (client, player);
    }

    [Fact]
    public async Task PayingFromWallet_ReducesBothDebtAndWallet()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlayerWithAsync(factory, client, walletMinorUnits: 10_000, debtMinorUnits: 3_000);

        var response = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 3_000), "debt-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();
        // Деньги не появляются из ниоткуда: списано с кошелька ровно столько, на сколько уменьшен долг.
        Assert.Equal(7_000, wallet!.WalletBalance.MinorUnits);
        Assert.Equal(0, wallet.DebtBalance.MinorUnits);
    }

    [Fact]
    public async Task PayingPartOfTheDebt_LeavesTheRest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlayerWithAsync(factory, client, walletMinorUnits: 10_000, debtMinorUnits: 3_000);

        var response = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 1_000), "debt-partial"));

        var wallet = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();
        Assert.Equal(9_000, wallet!.WalletBalance.MinorUnits);
        Assert.Equal(2_000, wallet.DebtBalance.MinorUnits);
    }

    [Fact]
    public async Task PayingMoreThanTheDebt_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlayerWithAsync(factory, client, walletMinorUnits: 10_000, debtMinorUnits: 3_000);

        var response = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 5_000), "debt-too-much"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PayingMoreThanTheWalletHolds_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlayerWithAsync(factory, client, walletMinorUnits: 1_000, debtMinorUnits: 3_000);

        var response = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 3_000), "debt-no-money"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Заморозка под бронь — отрицательная запись журнала, поэтому она уже вычтена из остатка.
    // Проверяется именно это: свободного у игрока ровно тысяча, и заплатить он может только её.
    // Вычесть «придержано» второй раз значило бы отказать человеку, у которого деньги есть.
    [Fact]
    public async Task HeldMoney_IsAlreadyOutsideTheBalanceAndNotSubtractedTwice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await PlayerWithAsync(factory, client, walletMinorUnits: 5_000, debtMinorUnits: 3_000, heldMinorUnits: 4_000);

        var tooMuch = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 3_000), "debt-held-too-much"));
        var affordable = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 1_000), "debt-held-ok"));

        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, affordable.StatusCode);
        var wallet = await affordable.Content.ReadFromJsonAsync<WalletSummaryDto>();
        Assert.Equal(0, wallet!.WalletBalance.MinorUnits);
        Assert.Equal(4_000, wallet.HeldBalance.MinorUnits);
        Assert.Equal(2_000, wallet.DebtBalance.MinorUnits);
        Assert.NotEqual(Guid.Empty, seeded.Player.PlayerId);
    }

    // Повторный запрос с тем же ключом — тот же ответ, а не второе списание.
    [Fact]
    public async Task RepeatingTheSameKey_DoesNotChargeTwice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlayerWithAsync(factory, client, walletMinorUnits: 10_000, debtMinorUnits: 3_000);
        await client.PostAsJsonAsync(Path, new PlayerDebtPaymentRequest(new MoneyDto("TJS", 1_000), "debt-repeat"));

        var second = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 1_000), "debt-repeat"));

        var wallet = await second.Content.ReadFromJsonAsync<WalletSummaryDto>();
        Assert.Equal(9_000, wallet!.WalletBalance.MinorUnits);
        Assert.Equal(2_000, wallet.DebtBalance.MinorUnits);
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Path,
            new PlayerDebtPaymentRequest(new MoneyDto("TJS", 1_000), "debt-anon"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
