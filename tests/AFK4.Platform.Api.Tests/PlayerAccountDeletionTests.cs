using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Удаление собственной учётной записи. Требование App Store к любому приложению с регистрацией
/// (5.1.1 v), и до сих пор его не было ни на сервере, ни в приложении.
///
/// Отказ при долге и при остатке — не перестраховка: деньги на кошельке принадлежат человеку, а
/// долг он должен клубу, и обнулять нажатием в телефоне ни то ни другое нельзя.
/// </summary>
public sealed class PlayerAccountDeletionTests
{
    private const string Pin = "1234";

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

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body is not null && body.TryGetValue("error", out var code) ? code : null;
    }

    [Fact]
    public async Task CleanAccount_IsDeletedAndTheNumberIsFreed()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var person = await db.PlatformPersons.SingleAsync(x => x.PhoneNumber != player.Phone);
        Assert.False(person.IsActive);
        Assert.Null(person.PinHash);
        // Номер освобождён: иначе «удалить аккаунт» навсегда забирало бы у человека его телефон.
        Assert.False(await db.PlatformPersons.AnyAsync(x => x.PhoneNumber == player.Phone));
        // И заменён так, что канонический номер («+» и цифры) им быть не может.
        Assert.DoesNotContain('+', person.PhoneNumber);
    }

    [Fact]
    public async Task DeletedAccount_IsNoLongerLetIn()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await client.DeleteAsync("/api/me");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Отдельно от предыдущего: вход перекрывает уже флаг «личность неактивна», и тот тест зелёный
    // даже с невыпотрошенными токенами. Здесь проверяется само удаление строк — выданные хеши
    // не должны переживать удаление учётной записи, которой они принадлежали.
    [Fact]
    public async Task DeletedAccount_LeavesNoIssuedTokensBehind()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        Guid personId;
        await using (var before = factory.Services.CreateAsyncScope())
        {
            var db = before.ServiceProvider.GetRequiredService<PlatformDbContext>();
            personId = await db.PlayerAccounts
                .Where(x => x.PlayerAccountId == player.PlayerId)
                .Select(x => x.PlatformPersonId!.Value)
                .SingleAsync();
            Assert.True(await db.PlatformPersonAccessTokens.AnyAsync(x => x.PlatformPersonId == personId));
        }

        await client.DeleteAsync("/api/me");

        await using var scope = factory.Services.CreateAsyncScope();
        var after = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await after.PlatformPersonAccessTokens.AnyAsync(x => x.PlatformPersonId == personId));
        Assert.False(await after.PlatformPersonRefreshTokens.AnyAsync(x => x.PlatformPersonId == personId));
    }

    // Журнал операций — бухгалтерия клуба, а не личные данные: он переживает удаление.
    [Fact]
    public async Task LedgerEntries_SurviveTheDeletion()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -5_000);

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, await db.LedgerEntries.CountAsync(x => x.PlayerAccountId == player.PlayerId));
    }

    [Fact]
    public async Task AccountWithMoneyLeft_IsNotDeleted()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5_000);

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("remaining_balance", await ErrorCodeAsync(response));
    }

    // Остаток ноль при замороженной под бронь тысяче — это не «денег нет».
    [Fact]
    public async Task AccountWithMoneyHeldUnderABooking_IsNotDeleted()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 1_000);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.ReservationHold, LedgerAccountTypeNames.Wallet, -1_000);

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("remaining_balance", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task AccountWithDebt_IsNotDeleted()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await SeedLedgerAsync(factory, player, LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 2_000);

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("outstanding_debt", await ErrorCodeAsync(response));
    }

    // Человек за ПК прямо сейчас: удалить себя посреди сессии значит оставить сессию без хозяина
    // и без того, с кого списать наигранное.
    [Fact]
    public async Task AccountWithARunningSession_IsNotDeleted()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var player = await TopUpTestData.SeedPlayerAsync(factory, Pin);
        await TopUpTestData.AuthenticateAsync(client, player, Pin);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(),
                OrganizationId = player.OrgId,
                BranchId = player.BranchId,
                SeatId = Guid.NewGuid(),
                PlayerAccountId = player.PlayerId,
                State = SessionStateNames.Active,
                RequestedAtUtc = DateTimeOffset.UtcNow,
                StartedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("active_session", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
