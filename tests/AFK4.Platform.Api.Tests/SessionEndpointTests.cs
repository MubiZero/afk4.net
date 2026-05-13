using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionEndpointTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid TargetSeatId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid TargetDeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task StartSession_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartSession_WithCashierPermission_StartsGuestSessionAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: false);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SessionStateNames.Active, body.Session.State);
        Assert.Equal(SeatId, body.Session.SeatId);
        Assert.Single(body.DeviceCommands);
        Assert.Equal("unlock", body.DeviceCommands[0].Type);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.StartSession, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(body.Session.SessionId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task StartSession_WithRealPrepaidBilling_AppendsGameplayCharge()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: false);
        var playerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var tariffVersion = await SeedBillingAsync(factory, playerAccountId);
        await SeedOpenShiftAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-real-prepaid-001",
                PlayerAccountId: playerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(dbContext.LedgerEntries, entry =>
            entry.EntryType == LedgerEntryTypeNames.GameplayCharge &&
            entry.AccountType == LedgerAccountTypeNames.Wallet &&
            entry.AmountMinorUnits < 0 &&
            entry.SessionId == body.Session.SessionId);
    }

    [Fact]
    public async Task StartSession_WithTechnicianRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.StartSession, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task StartSession_DuplicateIdempotencyKeyWithSameRequestReturnsSameSessionAndCommand()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: false);
        var request = new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1");

        var firstResponse = await client.PostAsJsonAsync($"/api/branches/{TestIds.BranchId:D}/sessions/start", request);
        var secondResponse = await client.PostAsJsonAsync($"/api/branches/{TestIds.BranchId:D}/sessions/start", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<SessionCommandResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Session.SessionId, second.Session.SessionId);
        Assert.Equal(first.DeviceCommands[0].CommandId, second.DeviceCommands[0].CommandId);
    }

    [Fact]
    public async Task StartSession_ReusingIdempotencyKeyWithDifferentSeatReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: true);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", "start-seat-1"));
        var conflict = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, TargetSeatId, 60, "manual-v1", "start-seat-1"));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task ExtendSession_WithCashierPermission_ReturnsRefreshedLeaseCommand()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: false);
        var started = await StartSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{started.Session.SessionId:D}/extend",
            new ExtendSessionRequest(AdditionalMinutes: 30, TariffRuleVersionId: "manual-v2", IdempotencyKey: "extend-session-1"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("manual-v2", body.Session.TariffRuleVersionId);
        Assert.Single(body.DeviceCommands);
        Assert.Equal("refresh-session-lease", body.DeviceCommands[0].Type);
    }

    [Fact]
    public async Task TransferSession_WithCashierPermission_ReturnsTransferredSession()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: true);
        var started = await StartSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{started.Session.SessionId:D}/transfer",
            new TransferSessionRequest(TargetSeatId, "transfer-session-1"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TargetSeatId, body.Session.SeatId);
        Assert.Equal(TargetDeviceId, body.Session.DeviceId);
        Assert.Equal(2, body.DeviceCommands.Count);
        Assert.Equal("lock", body.DeviceCommands[0].Type);
        Assert.Equal("unlock", body.DeviceCommands[1].Type);
    }

    [Fact]
    public async Task EndSession_WithCashierPermission_ReturnsEndingSession()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory, includeTargetSeat: false);
        var started = await StartSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{started.Session.SessionId:D}/end",
            new EndSessionRequest("operator-end", "end-session-1"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SessionStateNames.Ending, body.Session.State);
        Assert.Single(body.DeviceCommands);
        Assert.Equal("lock", body.DeviceCommands[0].Type);
    }

    private static async Task<SessionCommandResponse> StartSessionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(TestIds.OrganizationId, SeatId, 60, "manual-v1", $"start-seat-1-{Guid.NewGuid():N}"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body;
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory, bool includeTargetSeat)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = TestIds.DeviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            EnrolledAtUtc = Now,
            IsOnline = true,
            IsLocked = true
        });
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            AttachedAtUtc = Now
        });

        if (includeTargetSeat)
        {
            dbContext.Seats.Add(new SeatEntity
            {
                SeatId = TargetSeatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-002",
                SortOrder = 2,
                CreatedAtUtc = Now
            });
            dbContext.Devices.Add(new DeviceEntity
            {
                DeviceId = TargetDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-002",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = Now,
                IsOnline = true,
                IsLocked = true
            });
            dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = TargetSeatId,
                DeviceId = TargetDeviceId,
                AttachedAtUtc = Now
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<TariffVersionEntity> SeedBillingAsync(
        PlatformApiFactory factory,
        Guid playerAccountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        dbContext.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = playerAccountId,
            SessionId = null,
            PlayerPackageId = null,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 5000,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = LedgerEntryTypeNames.TopUp,
            Reason = "test seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            CreatedAtUtc = Now
        });
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };
        var tariffVersion = new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariff.TariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = Now.AddMinutes(-1),
            RetiredAtUtc = null,
            CreatedAtUtc = Now
        };
        dbContext.Tariffs.Add(tariff);
        dbContext.TariffVersions.Add(tariffVersion);
        await dbContext.SaveChangesAsync();

        return tariffVersion;
    }

    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }
}
