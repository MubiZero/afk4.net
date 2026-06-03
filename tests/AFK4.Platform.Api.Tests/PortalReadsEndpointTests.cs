using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PortalReadsEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId);

    // Seeds an active player + a PIN credential. Returns ids for further seeding.
    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Player One",
            PhoneNumber = "+992900000001",
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
            PhoneVerified = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, player);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task SeedLedgerAsync(
        PlatformApiFactory factory, Guid org, Guid branch, Guid player,
        string accountType, string entryType, long amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = org,
            BranchId = branch,
            PlayerAccountId = player,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test seed",
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Dashboard_ReturnsWalletAndDebtFromLedger()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedLedgerAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "wallet", "top_up", 10_000);
        await SeedLedgerAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "debt", "postpaid_debt", 2_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync("/api/me/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerDashboardDto>();
        Assert.Equal(10_000, dto!.WalletBalance.MinorUnits);
        Assert.Equal(2_500, dto.DebtBalance.MinorUnits);
        Assert.Null(dto.ActiveSession);
    }

    [Fact]
    public async Task Dashboard_WithOpenTab_ReturnsAccruedCost()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedActiveOpenSessionAsync(factory, p, pricePerMinute: 50, startedMinutesAgo: 40);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var dto = await (await client.GetAsync("/api/me/dashboard"))
            .Content.ReadFromJsonAsync<PlayerDashboardDto>();

        Assert.NotNull(dto!.ActiveSession);
        Assert.Equal("open", dto.ActiveSession!.DurationMode);
        Assert.Null(dto.ActiveSession.RemainingSeconds);
        // 40 min elapsed * 50/min = 2000 minor; allow +1 min rounding slack from wall-clock delta.
        Assert.InRange(dto.ActiveSession.AccruedCostMinorUnits!.Value, 2_000, 2_100);
        Assert.Equal("Seat 1", dto.ActiveSession.SeatName);
    }

    [Fact]
    public async Task Dashboard_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Seeds a seat, a tariff version, and an active OPEN session for the player.
    private static async Task SeedActiveOpenSessionAsync(
        PlatformApiFactory factory, SeededPlayer p, long pricePerMinute, int startedMinutesAgo)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seatId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            ZoneId = Guid.NewGuid(),
            Name = "Seat 1",
            SortOrder = 0,
            CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId,
            TariffId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = pricePerMinute,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = Now.AddYears(-1),
            CreatedAtUtc = Now.AddYears(-1)
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            SeatId = seatId,
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "player",
            PlayerAccountId = p.PlayerId,
            TariffRuleVersionId = tariffVersionId.ToString("D"),
            State = "active",
            RequestedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-startedMinutesAgo),
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-startedMinutesAgo),
            EndsAtUtc = null,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }
}
