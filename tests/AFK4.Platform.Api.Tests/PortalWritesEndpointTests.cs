using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PortalWritesEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    // Seeds an active player + a PIN credential with PhoneVerified=true.
    private static async Task<SeededPlayer> SeedPlayerAsync(
        PlatformApiFactory factory, string pin, bool phoneVerified = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99290000{player.ToString("N")[..4]}";

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = phoneVerified,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>()
            .HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, player, phone);
    }

    private static async Task AuthenticateAsync(
        HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    // Seeds an open shift for the staff-authenticated client (used in operator tests).
    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50_000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    // ---- A1: round-trip persist test ----

    [Fact]
    public async Task PaymentIntentEntity_CanBePersistedAndReloaded()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = "pending",
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = Now,
            FulfilledAtUtc = null
        });
        await db.SaveChangesAsync();

        var loaded = await db.PaymentIntents.FindAsync(intentId);
        Assert.NotNull(loaded);
        Assert.Equal(5_000, loaded!.AmountMinorUnits);
        Assert.Equal("pending", loaded.State);
        Assert.Equal("wallet_topup", loaded.Purpose);
        Assert.Equal("counter", loaded.Method);
        Assert.Null(loaded.FulfilledByLedgerEntryId);
    }
}
