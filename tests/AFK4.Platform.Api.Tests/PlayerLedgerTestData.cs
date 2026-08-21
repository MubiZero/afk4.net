using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Игрок с сетевым PIN и записями в журнале его клуба — ровно столько, сколько нужно, чтобы
/// спросить у сервера выписку от его имени.
/// </summary>
internal static class PlayerLedgerTestData
{
    private const string Phone = "+992900000061";
    private const string Pin = "1234";

    internal sealed record SeededLedgerPlayer(Guid OrgId, Guid BranchId, Guid PlayerId);

    public static async Task<SeededLedgerPlayer> SeedPlayerAsync(PlatformApiFactory factory)
    {
        var now = DateTimeOffset.Parse("2026-09-12T00:00:00Z");
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = player,
                OrganizationId = org,
                HomeBranchId = branch,
                DisplayName = "Игрок",
                PhoneNumber = Phone,
                PreferredLocale = "ru",
                IsActive = true,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, Phone, Pin);
        return new SeededLedgerPlayer(org, branch, player);
    }

    public static async Task AuthenticateAsync(HttpClient client, SeededLedgerPlayer player)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(player.OrgId, Phone, Pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    /// <summary>Одна запись журнала. Возвращает её идентификатор — по нему вешается реверс.</summary>
    public static async Task<Guid> AddAsync(
        PlatformApiFactory factory,
        SeededLedgerPlayer player,
        string entryType,
        long amountMinorUnits,
        DateTimeOffset createdAtUtc,
        Guid? reverses = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var id = Guid.NewGuid();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = id,
            OrganizationId = player.OrgId,
            BranchId = player.BranchId,
            PlayerAccountId = player.PlayerId,
            EntryType = entryType,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test seed",
            ReversesLedgerEntryId = reverses,
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc
        });
        await db.SaveChangesAsync();
        return id;
    }
}
