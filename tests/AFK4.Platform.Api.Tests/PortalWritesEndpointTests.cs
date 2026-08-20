using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;
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
        // Номер должен быть из одних цифр: вход нормализует его до E.164, и шестнадцатеричные
        // буквы из Guid оставили бы меньше одиннадцати цифр — такой номер отвергается.
        var phone = $"+99290000{(uint)player.GetHashCode() % 10_000:D4}";

        // IOrganizationEntitlements.IsEnabledAsync anchors on the Organizations row: without it,
        // the org is "unknown" and every feature (online_booking, online_topup, ...) resolves to
        // disabled.
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = org,
            Name = "Portal Test Org",
            CreatedAtUtc = Now
        });

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

        // Тесты этого файла про права, состояния и конфликты, а не про правила приёма гостей:
        // филиал берёт брони без предоплаты и без потолка на новичка.
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(org, branch, Now));

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(
            factory, player, phone, pin, phoneVerified: phoneVerified);
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

    // ---- A2: POST /api/me/wallet/top-up-intent ----

    [Fact]
    public async Task CreateTopUpIntent_WithVerifiedPhone_CreatesPendingIntent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.NotNull(dto);
        Assert.Equal(10_000, dto!.AmountMinorUnits);
        Assert.Equal("TJS", dto.CurrencyCode);
        Assert.Equal("pending", dto.State);
        Assert.Equal("wallet_topup", dto.Purpose);
        Assert.Equal("counter", dto.Method);
        Assert.False(dto.IsExpired);
        Assert.Null(dto.FulfilledAtUtc);

        // Verify persisted to DB
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.FindAsync(dto.PaymentIntentId);
        Assert.NotNull(intent);
        Assert.Equal(p.PlayerId, intent!.PlayerAccountId);
        Assert.Equal(p.BranchId, intent.BranchId);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithExplicitCurrency_UsesThatCurrency()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("TJS", dto!.CurrencyCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithUnverifiedPhone_StillCreatesPendingIntent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234", phoneVerified: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("pending", dto!.State);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithZeroAmount_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(0, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithNegativeAmount_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(-100, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_Isolation_IntentTiedToTokenPlayer()
    {
        // Two different players; each creates an intent; each sees only their own.
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var r1 = await client1.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(3_000, null));
        var dto1 = await r1.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();

        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        var r2 = await client2.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(7_000, null));
        var dto2 = await r2.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent1 = await db.PaymentIntents.FindAsync(dto1!.PaymentIntentId);
        var intent2 = await db.PaymentIntents.FindAsync(dto2!.PaymentIntentId);
        Assert.Equal(p1.PlayerId, intent1!.PlayerAccountId);
        Assert.Equal(p2.PlayerId, intent2!.PlayerAccountId);
    }

    // ---- A3: GET /api/me/wallet/top-up-intents ----

    private static async Task<Guid> SeedPaymentIntentAsync(
        PlatformApiFactory factory,
        Guid orgId, Guid branchId, Guid playerId,
        string state, DateTimeOffset createdAtUtc, long amountMinorUnits = 5_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            BranchId = branchId,
            AmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = createdAtUtc,
            FulfilledAtUtc = state == "fulfilled" ? createdAtUtc.AddMinutes(5) : null
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    [Fact]
    public async Task ListTopUpIntents_ReturnsOwnIntents_NewestFirst()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "pending", t1, 3_000);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "fulfilled", t2, 7_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync("/api/me/wallet/top-up-intents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Equal(7_000, list[0].AmountMinorUnits);   // newest first (t2)
        Assert.Equal(3_000, list[1].AmountMinorUnits);   // oldest (t1)
    }

    [Fact]
    public async Task ListTopUpIntents_ExpiredFlag_ComputedCorrectly()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        // Pending but created >24h ago → IsExpired = true
        var oldTs = DateTimeOffset.UtcNow.AddHours(-25);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "pending", oldTs, 1_000);
        // Fulfilled but old → IsExpired = false (not pending)
        var veryOldTs = DateTimeOffset.UtcNow.AddHours(-48);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "fulfilled", veryOldTs, 2_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var list = await (await client.GetAsync("/api/me/wallet/top-up-intents"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();

        var expired = list!.Single(x => x.AmountMinorUnits == 1_000);
        var fulfilled = list!.Single(x => x.AmountMinorUnits == 2_000);
        Assert.True(expired.IsExpired);
        Assert.False(fulfilled.IsExpired);
    }

    [Fact]
    public async Task ListTopUpIntents_DoesNotReturnOtherPlayersIntents()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");
        await SeedPaymentIntentAsync(factory, p2.OrgId, p2.BranchId, p2.PlayerId, "pending", DateTimeOffset.UtcNow, 9_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p1.OrgId, p1.Phone, "1111");

        var list = await (await client.GetAsync("/api/me/wallet/top-up-intents"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();

        Assert.Empty(list!);
    }

    [Fact]
    public async Task ListTopUpIntents_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/wallet/top-up-intents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- A4: POST /api/wallet/top-up-intents/{id}/fulfil ----

    private static async Task<(SeededPlayer Player, Guid IntentId)> SeedFulfilScenarioAsync(
        PlatformApiFactory factory, string state = "pending", int createdHoursAgo = 1)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var playerId = Guid.NewGuid();
        var phone = $"+99291000{(uint)playerId.GetHashCode() % 10_000:D4}";
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Intent Player",
            PhoneNumber = phone,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var intentId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-createdHoursAgo);
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = createdAt,
            FulfilledAtUtc = state == "fulfilled" ? createdAt.AddMinutes(5) : null
        });
        await db.SaveChangesAsync();

        var p = new SeededPlayer(TestIds.OrganizationId, TestIds.BranchId, playerId, phone);
        return (p, intentId);
    }

    [Fact]
    public async Task FulfilIntent_WithCashier_WritesWalletCreditAndFlipsToFulfilled()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        var (p, intentId) = await SeedFulfilScenarioAsync(factory);
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.NotNull(dto);
        Assert.Equal("fulfilled", dto!.State);
        Assert.NotNull(dto.FulfilledAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entries = await db.LedgerEntries
            .Where(e => e.PlayerAccountId == p.PlayerId && e.EntryType == "top_up")
            .ToListAsync();
        Assert.Single(entries);
        Assert.Equal(5_000, entries[0].AmountMinorUnits);
    }

    [Fact]
    public async Task FulfilIntent_Idempotent_DoesNotDoubleCredit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        var (p, intentId) = await SeedFulfilScenarioAsync(factory, state: "pending");
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        var r1 = await client.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{intentId}/fulfil", new { });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        var r2 = await client.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{intentId}/fulfil", new { });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entries = await db.LedgerEntries
            .Where(e => e.PlayerAccountId == p.PlayerId && e.EntryType == "top_up")
            .ToListAsync();
        Assert.Single(entries);
    }

    [Fact]
    public async Task FulfilIntent_WhenExpired_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        var (_, intentId) = await SeedFulfilScenarioAsync(factory, state: "pending", createdHoursAgo: 25);
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FulfilIntent_WhenNotFound_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{Guid.NewGuid()}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FulfilIntent_RequiresTopUpWalletPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var (_, intentId) = await SeedFulfilScenarioAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- B1: IReservationService.CreateOnlineAsync + CancelOnlineAsync ----

    private static async Task<(Guid SeatId, Guid ZoneId)> SeedSeatAsync(
        PlatformApiFactory factory, Guid orgId, Guid branchId, string name = "PC-01")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Main",
            SortOrder = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = zoneId,
            Name = name,
            SortOrder = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return (seatId, zoneId);
    }

    [Fact]
    public async Task CreateOnlineAsync_CreatesReservation_WithPendingState_AndOnlineSource()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Online Player",
            PhoneNumber = "+992911000001",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(orgId, branchId, Now));
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);

        var result = await svc.CreateOnlineAsync(
            playerId,
            orgId,
            branchId,
            new CreatePlayerReservationRequest(null, startsAt, endsAt, "online request"),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, result.Response!.State);
        Assert.Equal(ReservationSourceNames.Online, result.Response.Source);
        Assert.Equal(playerId, result.Response.PlayerAccountId);
        Assert.Equal("Online Player", result.Response.CustomerName);
    }

    [Fact]
    public async Task CreateOnlineAsync_WithConflictingSeat_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        var zoneId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "P", PhoneNumber = "+992911000002", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId, OrganizationId = orgId, BranchId = branchId,
            Name = "Z", SortOrder = 1, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId, OrganizationId = orgId, BranchId = branchId,
            ZoneId = zoneId, Name = "S1", SortOrder = 1, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(orgId, branchId, Now));
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);
        var req = new CreatePlayerReservationRequest(seatId, startsAt, endsAt, null);

        // First booking succeeds
        var r1 = await svc.CreateOnlineAsync(playerId, orgId, branchId, req, default);
        Assert.True(r1.Succeeded);

        // Second overlapping booking conflicts
        var r2 = await svc.CreateOnlineAsync(playerId, orgId, branchId,
            new CreatePlayerReservationRequest(seatId, startsAt.AddMinutes(30), endsAt, null),
            default);
        Assert.True(r2.Conflict);
    }

    [Fact]
    public async Task CancelOnlineAsync_OwnReservation_FlipsToCancelled()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "Canceller", PhoneNumber = "+992911000003", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(orgId, branchId, Now));
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var created = await svc.CreateOnlineAsync(
            playerId, orgId, branchId,
            new CreatePlayerReservationRequest(null, DateTimeOffset.UtcNow.AddHours(3), DateTimeOffset.UtcNow.AddHours(4), null),
            default);
        Assert.True(created.Succeeded);

        var cancelled = await svc.CancelOnlineAsync(created.Response!.ReservationId, playerId, default);
        Assert.True(cancelled.Succeeded);
        Assert.Equal(ReservationStateNames.Cancelled, cancelled.Response!.State);
    }

    [Fact]
    public async Task CancelOnlineAsync_ForeignReservation_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = ownerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "Owner", PhoneNumber = "+992911000004", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.BranchBookingSettings.Add(BranchBookingSettingsTestData.AcceptsAnyGuest(orgId, branchId, Now));
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var created = await svc.CreateOnlineAsync(
            ownerId, orgId, branchId,
            new CreatePlayerReservationRequest(null, DateTimeOffset.UtcNow.AddHours(3), DateTimeOffset.UtcNow.AddHours(4), null),
            default);
        Assert.True(created.Succeeded);

        // otherPlayerId tries to cancel the owner's reservation
        var result = await svc.CancelOnlineAsync(created.Response!.ReservationId, otherPlayerId, default);
        Assert.True(result.NotFound);
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

    // ---- B2: POST /api/me/reservations ----

    [Fact]
    public async Task BookOnline_WithVerifiedPhone_CreatesPendingReservation()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var (seatId, _) = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, endsAt, "window seat please"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.NotNull(dto);
        Assert.Equal("pending", dto!.State);
        Assert.Equal(seatId, dto.SeatId);
        Assert.Equal("PC-01", dto.SeatName);

        // Verify reservation belongs to the token's player, not a stranger
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entity = await db.Reservations.FindAsync(dto.ReservationId);
        Assert.NotNull(entity);
        Assert.Equal(p.PlayerId, entity!.PlayerAccountId);
        Assert.Equal("Test Player", entity.CustomerName);
    }

    [Fact]
    public async Task BookOnline_WithUnverifiedPhone_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234", phoneVerified: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithEndBeforeStart_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var now = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, now.AddHours(2), now.AddHours(1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithStartInPast_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, past, past.AddHours(1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithOverlappingSeat_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var (seatId, _) = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);

        var r1 = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, endsAt, null));
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Overlapping booking on the same seat
        var r2 = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt.AddMinutes(30), endsAt.AddMinutes(30), null));
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- B3: GET /api/me/reservations + DELETE /api/me/reservations/{id} ----

    [Fact]
    public async Task ListReservations_ReturnsOwnReservations_Only()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");
        var (seatId, _) = await SeedSeatAsync(factory, p1.OrgId, p1.BranchId);

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        await client1.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));

        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        // p2 has its own org/branch; list only its reservations
        var list2 = await (await client2.GetAsync("/api/me/reservations"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerReservationDto>>();
        Assert.Empty(list2!);

        var list1 = await (await client1.GetAsync("/api/me/reservations"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerReservationDto>>();
        Assert.Single(list1!);
        Assert.Equal(seatId, list1![0].SeatId);
        Assert.Equal("PC-01", list1[0].SeatName);
    }

    [Fact]
    public async Task ListReservations_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_OwnReservation_Returns200WithCancelledState()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var created = await (await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null)))
            .Content.ReadFromJsonAsync<PlayerReservationDto>();

        var cancelResponse = await client.DeleteAsync($"/api/me/reservations/{created!.ReservationId}");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var dto = await cancelResponse.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.Equal("cancelled", dto!.State);
    }

    [Fact]
    public async Task CancelReservation_ForeignReservation_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var created = await (await client1.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null)))
            .Content.ReadFromJsonAsync<PlayerReservationDto>();

        // p2 tries to cancel p1's reservation
        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        var cancelResponse = await client2.DeleteAsync($"/api/me/reservations/{created!.ReservationId}");

        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/me/reservations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
