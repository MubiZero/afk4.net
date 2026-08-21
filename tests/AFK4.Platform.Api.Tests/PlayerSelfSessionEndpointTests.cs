using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PlayerSelfSessionEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SelfStartContext(
        Guid OrgId, Guid BranchId, Guid PlayerId, string Phone,
        Guid DeviceId, Guid SeatId, string TariffRuleVersionId);

    /// <summary>
    /// Seeds: player + PIN credential + wallet ledger entry + seat + approved device
    /// + active DeviceSeatAssignment + TariffVersion.
    /// The tariff: 1000 minor/min, min=1 min. At 60 min the charge = 60_000 minor.
    /// walletMinorUnits=100_000 comfortably covers it; 100 does not.
    /// </summary>
    private static async Task<SelfStartContext> SeedSelfStartContextAsync(
        PlatformApiFactory factory, long walletMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var phone = TestPhones.Next();
        var deviceId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();

        // Player account
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Self-Start Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });

        // Wallet ledger entry (top-up)
        if (walletMinorUnits > 0)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(),
                OrganizationId = orgId,
                BranchId = branchId,
                PlayerAccountId = playerId,
                EntryType = "top_up",
                AccountType = "wallet",
                AmountMinorUnits = walletMinorUnits,
                CurrencyCode = "TJS",
                Description = "test top_up",
                Reason = "test seed",
                CreatedByStaffUserId = Guid.NewGuid(),
                CreatedAtUtc = Now
            });
        }

        // Seat
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = Guid.NewGuid(),
            Name = "PC-Self",
            SortOrder = 10,
            CreatedAtUtc = Now
        });

        // Approved device
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = orgId,
            BranchId = branchId,
            MachineName = "PC-SELF-01",
            DisplayName = "Self-Start PC",
            DevicePublicKey = "test-key",
            Role = "gaming_pc",
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            AgentVersion = "1.0.0",
            ShellVersion = "1.0.0",
            EnrolledAtUtc = Now.AddDays(-30)
        });

        // Active seat assignment
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = deviceId,
            AttachedAtUtc = Now.AddDays(-1),
            DetachedAtUtc = null
        });

        // Tariff version: 1000 minor/min, no minimum, no rounding
        // => 60 min => 60_000 minor. 100_000 covers it, 100 does not.
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId,
            TariffId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 1000,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = Now.AddYears(-1),
            CreatedAtUtc = Now.AddYears(-1)
        });

        // Open shift is required by session billing validation
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            OpenedByStaffUserId = Guid.NewGuid(),
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 0,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now.AddHours(-1)
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, playerId, phone, "1234");

        return new SelfStartContext(
            orgId, branchId, playerId, phone,
            deviceId, seatId, tariffVersionId.ToString("D"));
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

    [Fact]
    public async Task SelfStart_WithSufficientWallet_StartsSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId);
        Assert.Equal("active", session.State);
        Assert.Equal(ctx.SeatId, session.SeatId);
        // Человек сел сам — и назвать это посадкой оператора нельзя: у смены и у отчётов
        // «кто посадил» разные ответы на эти два случая.
        Assert.Equal(SessionOriginNames.SelfService, session.Origin);
    }

    [Fact]
    public async Task SelfStart_WithInsufficientWallet_Returns409InsufficientBalance()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_balance", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SelfStart_WithoutToken_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(Guid.NewGuid(), Guid.NewGuid().ToString("D"), 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SelfExtend_OwnedActiveSession_WithWallet_Extends()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var start = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));
        start.EnsureSuccessStatusCode();
        Guid sessionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            sessionId = (await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId)).SessionId;
        }

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SelfExtend_ForeignSession_Returns404()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{Guid.NewGuid()}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
