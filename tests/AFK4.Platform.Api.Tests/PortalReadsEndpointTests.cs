using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PortalReadsEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId);

    // Seeds an active player + a network PIN on their person. Returns ids for further seeding.
    private static async Task<SeededPlayer> SeedPlayerAsync(
        PlatformApiFactory factory, string pin, string phone = "+992900000001")
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
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
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

    // Seeds an ENDED session for the player, with optional checkout receipt and
    // optional attached POS sale. Returns the sessionId.
    private static async Task<Guid> SeedEndedVisitAsync(
        PlatformApiFactory factory, SeededPlayer p, string seatName,
        DateTimeOffset endedAtUtc, long? receiptTotal, long attachedPosTotal)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seatId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            ZoneId = Guid.NewGuid(), Name = seatName, SortOrder = 0, CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            SeatId = seatId, DeviceId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "player", PlayerAccountId = p.PlayerId,
            TariffRuleVersionId = Guid.NewGuid().ToString("D"), State = "ended",
            RequestedAtUtc = endedAtUtc.AddHours(-1), StartedAtUtc = endedAtUtc.AddHours(-1),
            EndsAtUtc = null, EndedAtUtc = endedAtUtc, UpdatedAtUtc = endedAtUtc
        });
        if (attachedPosTotal > 0)
        {
            db.PosSales.Add(new PosSaleEntity
            {
                PosSaleId = Guid.NewGuid(), OrganizationId = p.OrgId, BranchId = p.BranchId,
                ShiftId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
                PlayerAccountId = p.PlayerId, SessionId = sessionId, State = "paid",
                CurrencyCode = "TJS", TotalMinorUnits = attachedPosTotal,
                CreatedAtUtc = endedAtUtc, PaidAtUtc = endedAtUtc
            });
        }
        if (receiptTotal is not null)
        {
            db.Receipts.Add(new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(), OrganizationId = p.OrgId, BranchId = p.BranchId,
                SessionId = sessionId, PosSaleId = null,
                ReceiptNumber = "POS-20260603-0001", ReceiptType = "session_checkout",
                CurrencyCode = "TJS", TotalMinorUnits = receiptTotal.Value, CreatedAtUtc = endedAtUtc
            });
        }
        await db.SaveChangesAsync();
        return sessionId;
    }

    [Fact]
    public async Task Visits_ReturnsOwnEndedSessions_NewestFirst_WithDerivedTotals()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-2), receiptTotal: 5_000, attachedPosTotal: 1_500);
        await SeedEndedVisitAsync(factory, p, "Seat B", Now.AddDays(-1), receiptTotal: null, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();

        Assert.Equal(2, page!.Items.Count);
        Assert.Equal("Seat B", page.Items[0].SeatName);     // newest first
        Assert.False(page.Items[0].HasReceipt);
        var seatA = page.Items[1];
        Assert.True(seatA.HasReceipt);
        Assert.Equal(5_000, seatA.GrandTotalMinorUnits);
        Assert.Equal(1_500, seatA.PosTotalMinorUnits);
        Assert.Equal(3_500, seatA.TimeChargeMinorUnits);    // grand - pos
    }

    [Fact]
    public async Task Visits_DoesNotReturnOtherPlayersSessions()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999", "+992900000002");
        await SeedEndedVisitAsync(factory, other, "Seat X", Now.AddDays(-1), receiptTotal: 9_000, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();

        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Visits_Paginates_WithCursor()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        for (var i = 0; i < 25; i++)
        {
            await SeedEndedVisitAsync(factory, p, $"Seat {i}", Now.AddMinutes(-i), receiptTotal: 1_000, attachedPosTotal: 0);
        }
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var first = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();
        Assert.Equal(20, first!.Items.Count);
        Assert.NotNull(first.NextCursor);

        var second = await (await client.GetAsync($"/api/me/visits?cursor={Uri.EscapeDataString(first.NextCursor!)}"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();
        Assert.Equal(5, second!.Items.Count);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Receipt_ForOwnSession_ReturnsReceiptWithBreakdown()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var sessionId = await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-1), receiptTotal: 5_000, attachedPosTotal: 1_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{sessionId}/receipt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerVisitReceiptDto>();
        Assert.Equal("POS-20260603-0001", dto!.ReceiptNumber);
        Assert.Equal(5_000, dto.GrandTotalMinorUnits);
        Assert.Equal(1_500, dto.PosTotalMinorUnits);
        Assert.Equal(3_500, dto.TimeChargeMinorUnits);
    }

    [Fact]
    public async Task Receipt_ForOtherPlayersSession_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999", "+992900000002");
        var otherSession = await SeedEndedVisitAsync(factory, other, "Seat X", Now.AddDays(-1), receiptTotal: 9_000, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{otherSession}/receipt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_WhenNoReceiptExists_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var sessionId = await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-1), receiptTotal: null, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{sessionId}/receipt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task SeedStandalonePurchaseAsync(
        PlatformApiFactory factory, SeededPlayer p, DateTimeOffset createdAtUtc,
        long total, string productName, int quantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var posSaleId = Guid.NewGuid();
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = posSaleId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            ShiftId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
            PlayerAccountId = p.PlayerId, SessionId = null, State = "paid",
            CurrencyCode = "TJS", TotalMinorUnits = total,
            CreatedAtUtc = createdAtUtc, PaidAtUtc = createdAtUtc
        });
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(), PosSaleId = posSaleId,
            ProductId = Guid.NewGuid(), ProductName = productName, Quantity = quantity,
            CurrencyCode = "TJS", UnitPriceMinorUnits = total / quantity,
            LineTotalMinorUnits = total, TracksStock = false, AllowNegativeStock = true
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Purchases_ReturnsOnlyStandaloneOwnSales_WithLines()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedStandalonePurchaseAsync(factory, p, Now.AddDays(-1), 3_000, "Cola", 2);
        // Session-attached sale must NOT appear in purchases:
        await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-2), receiptTotal: 5_000, attachedPosTotal: 1_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/purchases"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerPurchaseDto>>();

        Assert.Single(page!.Items);
        var purchase = page.Items[0];
        Assert.Equal(3_000, purchase.TotalMinorUnits);
        Assert.Single(purchase.Lines);
        Assert.Equal("Cola", purchase.Lines[0].ProductName);
        Assert.Equal(2, purchase.Lines[0].Quantity);
    }

    [Fact]
    public async Task Purchases_DoesNotReturnOtherPlayersSales()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999", "+992900000002");
        await SeedStandalonePurchaseAsync(factory, other, Now.AddDays(-1), 7_000, "Pizza", 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/purchases"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerPurchaseDto>>();

        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task PatchProfile_UpdatesLocaleAndMarketing()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest("en", true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        Assert.Equal("en", dto!.PreferredLocale);
        Assert.True(dto.MarketingOptIn);
    }

    [Fact]
    public async Task PatchProfile_NullFields_LeaveValuesUnchanged()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");   // seeded locale "ru", marketing false
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest(null, null));

        var dto = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        Assert.Equal("ru", dto!.PreferredLocale);
        Assert.False(dto.MarketingOptIn);
    }

    [Fact]
    public async Task PatchProfile_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest("en", true));
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
