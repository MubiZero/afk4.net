using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests;

public sealed class ReservationEndpointTests
{
    private static readonly DateTimeOffset BookingDay = DateTimeOffset.Parse("2026-05-21T00:00:00Z");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid SeatOneId = Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111");
    private static readonly Guid SeatTwoId = Guid.Parse("aaaaaaaa-2222-4222-8222-222222222222");

    [Fact]
    public async Task StartReservationSession_WithoutStaffToken_ReturnsUnauthorized()
    {
        var coordinator = new StubReservationSessionCoordinator(StartSuccess());
        await using var factory = CreateStartFactory(coordinator, StaffContextWithPermissions(
            OrganizationPermissionNames.ManageReservations,
            OrganizationPermissionNames.StartSession));
        using var client = factory.CreateClient();
        var reservation = await SeedReservationAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            StartRequest(reservation.Version));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(coordinator.LastRequest);
    }

    [Theory]
    [InlineData(OrganizationPermissionNames.ManageReservations)]
    [InlineData(OrganizationPermissionNames.StartSession)]
    public async Task StartReservationSession_RequiresReservationManagementAndSessionStartPermissions(
        string grantedPermission)
    {
        var coordinator = new StubReservationSessionCoordinator(StartSuccess());
        await using var factory = CreateStartFactory(coordinator, StaffContextWithPermissions(grantedPermission));
        using var client = AuthorizedClient(factory);
        var reservation = await SeedReservationAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            StartRequest(reservation.Version));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(coordinator.LastRequest);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal("reservations.session.start", audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
    }

    [Fact]
    public async Task StartReservationSession_HidesReservationsOutsideAuthenticatedTenant()
    {
        var coordinator = new StubReservationSessionCoordinator(StartSuccess());
        var foreignContext = StaffContextWithPermissions(
            OrganizationPermissionNames.ManageReservations,
            OrganizationPermissionNames.StartSession) with { OrganizationId = Guid.NewGuid() };
        await using var factory = CreateStartFactory(coordinator, foreignContext);
        using var client = AuthorizedClient(factory);
        var reservation = await SeedReservationAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            StartRequest(reservation.Version) with { OrganizationId = foreignContext.OrganizationId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(coordinator.LastRequest);
    }

    [Fact]
    public async Task StartReservationSession_RejectsStaffOutsideReservationBranch()
    {
        var coordinator = new StubReservationSessionCoordinator(StartSuccess());
        var staff = StaffContextWithPermissions(
            OrganizationPermissionNames.ManageReservations,
            OrganizationPermissionNames.StartSession) with { BranchIds = new HashSet<Guid>() };
        await using var factory = CreateStartFactory(coordinator, staff);
        using var client = AuthorizedClient(factory);
        var reservation = await SeedReservationAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            StartRequest(reservation.Version));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(coordinator.LastRequest);
    }

    [Theory]
    [InlineData("reservation_not_found", "missing", HttpStatusCode.NotFound, null)]
    [InlineData("player_required_for_wallet", "player required", HttpStatusCode.BadRequest, null)]
    [InlineData("reservation_expired", "expired", HttpStatusCode.Conflict, 7)]
    [InlineData("version_conflict", "stale", HttpStatusCode.Conflict, 8)]
    public async Task StartReservationSession_MapsCoordinatorFailuresToStableStatusAndBody(
        string code,
        string error,
        HttpStatusCode expectedStatus,
        int? currentVersion)
    {
        var result = expectedStatus switch
        {
            HttpStatusCode.NotFound => ReservationSessionStartResult.Missing(code, error),
            HttpStatusCode.Conflict => ReservationSessionStartResult.RequestConflict(code, error, currentVersion),
            _ => ReservationSessionStartResult.Invalid(code, error)
        };
        var coordinator = new StubReservationSessionCoordinator(result);
        await using var factory = CreateStartFactory(coordinator, StaffContextWithPermissions(
            OrganizationPermissionNames.ManageReservations,
            OrganizationPermissionNames.StartSession));
        using var client = AuthorizedClient(factory);
        var reservation = await SeedReservationAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            StartRequest(reservation.Version));

        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReservationConflictBody>();
        Assert.NotNull(body);
        Assert.Equal(code, body.Code);
        Assert.Equal(error, body.Error);
        Assert.Equal(currentVersion, body.CurrentVersion);
    }

    [Fact]
    public async Task StartReservationSession_PassesActorAndCompApprovalWithoutPostCommitEndpointAudit()
    {
        var coordinator = new StubReservationSessionCoordinator(StartSuccess());
        var staff = StaffContextWithPermissions(
            OrganizationPermissionNames.ManageReservations,
            OrganizationPermissionNames.StartSession,
            OrganizationPermissionNames.ApproveMoneyAction);
        await using var factory = CreateStartFactory(coordinator, staff);
        using var client = AuthorizedClient(factory);
        var reservation = await SeedReservationAsync(factory);
        var request = StartRequest(reservation.Version);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{reservation.ReservationId}/start-session",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StartReservationSessionResponse>();
        Assert.NotNull(body);
        Assert.Equal(coordinator.Result.Response!.Reservation, body.Reservation);
        Assert.Equal(coordinator.Result.Response.Session.Session, body.Session.Session);
        Assert.Equal(coordinator.Result.Response.Session.IdempotencyKey, body.Session.IdempotencyKey);
        Assert.Empty(body.Session.DeviceCommands);
        Assert.Equal(reservation.ReservationId, coordinator.LastReservationId);
        Assert.Equal(staff.StaffUserId, coordinator.LastActorStaffUserId);
        Assert.True(coordinator.LastActorCanApproveComp);
        Assert.Equal(request, coordinator.LastRequest);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
    }

    [Fact]
    public async Task GetReservations_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReservations_WithTechnicianRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewReservations, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task ReservationWorkflow_CreatesConfirmsUpdatesSeatsCancelsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAsync(factory);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations",
            new CreateReservationRequest(
                TestIds.OrganizationId,
                PlayerAccountId: null,
                SeatOneId,
                CustomerName: "Aziz P.",
                PhoneNumber: "+992900000001",
                StartsAtUtc: BookingDay.AddHours(16),
                DurationMinutes: 60,
                Source: ReservationSourceNames.Online,
                Note: "online request"));
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(ReservationStateNames.Pending, created.State);
        Assert.Equal("PC-01", created.SeatName);
        Assert.Equal("Main", created.ZoneName);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{created.ReservationId}/confirm",
            new ConfirmReservationRequest(TestIds.OrganizationId, created.Version));
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.NotNull(confirmed);
        Assert.Equal(ReservationStateNames.Confirmed, confirmed.State);
        Assert.Equal(created.Version + 1, confirmed.Version);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{created.ReservationId}",
            new UpdateReservationRequest(
                TestIds.OrganizationId,
                PlayerAccountId: null,
                SeatTwoId,
                CustomerName: "Aziz Prime",
                PhoneNumber: "+992900000002",
                StartsAtUtc: BookingDay.AddHours(17),
                DurationMinutes: 90,
                Source: ReservationSourceNames.Operator,
                Note: "front desk move",
                ExpectedVersion: confirmed.Version));
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Aziz Prime", updated.CustomerName);
        Assert.Equal(SeatTwoId, updated.SeatId);
        Assert.Equal(90, updated.DurationMinutes);
        Assert.Equal(confirmed.Version + 1, updated.Version);

        var listResponse = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations?fromUtc=2026-05-21T00:00:00Z&toUtc=2026-05-21T23:59:59Z");
        var list = await listResponse.Content.ReadFromJsonAsync<ReservationSearchResultDto>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(list);
        Assert.Equal(created.ReservationId, Assert.Single(list.Reservations).ReservationId);

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{created.ReservationId}/cancel",
            new CancelReservationRequest(TestIds.OrganizationId, "client left", updated.Version));
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.NotNull(cancelled);
        Assert.Equal(ReservationStateNames.Cancelled, cancelled.State);
        Assert.Equal("client left", cancelled.CancelReason);
        Assert.Equal(updated.Version + 1, cancelled.Version);

        var seatCreateResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations",
            new CreateReservationRequest(
                TestIds.OrganizationId,
                PlayerAccountId: null,
                SeatOneId,
                CustomerName: "Walk-in guest",
                PhoneNumber: null,
                StartsAtUtc: BookingDay.AddHours(19),
                DurationMinutes: 60,
                Source: ReservationSourceNames.Operator,
                Note: "walk-in"));
        var seatCandidate = await seatCreateResponse.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal(HttpStatusCode.OK, seatCreateResponse.StatusCode);
        Assert.NotNull(seatCandidate);

        var seatResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{seatCandidate.ReservationId}/seat",
            new SeatReservationRequest(TestIds.OrganizationId, seatCandidate.Version));
        var seated = await seatResponse.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.Equal(HttpStatusCode.OK, seatResponse.StatusCode);
        Assert.NotNull(seated);
        Assert.Equal(ReservationStateNames.Seated, seated.State);
        Assert.Equal(seatCandidate.Version + 1, seated.Version);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(2, await dbContext.Reservations.CountAsync());
        var audits = await dbContext.AuditRecords.ToListAsync();
        Assert.Contains(audits, audit => audit.Action == AuditActionNames.CreateReservation && audit.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(audits, audit => audit.Action == AuditActionNames.UpdateReservation && audit.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(audits, audit => audit.Action == AuditActionNames.SeatReservation && audit.Outcome == AuditOutcome.Succeeded);
        Assert.Contains(audits, audit => audit.Action == AuditActionNames.CancelReservation && audit.Outcome == AuditOutcome.Succeeded);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("confirm")]
    [InlineData("cancel")]
    [InlineData("seat")]
    public async Task ReservationMutation_WithStaleVersion_ReturnsAuthoritativeConflictAndLeavesEntityUnchanged(
        string mutation)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAsync(factory);
        var current = await SeedReservationAsync(factory);

        using var response = mutation switch
        {
            "update" => await client.PatchAsJsonAsync(
                $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{current.ReservationId}",
                new UpdateReservationRequest(
                    TestIds.OrganizationId,
                    PlayerAccountId: null,
                    SeatTwoId,
                    CustomerName: "Changed by stale operator",
                    PhoneNumber: "+992900000099",
                    StartsAtUtc: BookingDay.AddHours(18),
                    DurationMinutes: 120,
                    Source: ReservationSourceNames.Operator,
                    Note: "stale update",
                    ExpectedVersion: current.Version - 1)),
            "confirm" => await client.PostAsJsonAsync(
                $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{current.ReservationId}/confirm",
                new ConfirmReservationRequest(TestIds.OrganizationId, current.Version - 1)),
            "cancel" => await client.PostAsJsonAsync(
                $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{current.ReservationId}/cancel",
                new CancelReservationRequest(TestIds.OrganizationId, "stale cancellation", current.Version - 1)),
            "seat" => await client.PostAsJsonAsync(
                $"/api/organizations/{TestIds.OrganizationId:D}/reservations/{current.ReservationId}/seat",
                new SeatReservationRequest(TestIds.OrganizationId, current.Version - 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = await response.Content.ReadFromJsonAsync<ReservationConflictBody>();
        Assert.NotNull(conflict);
        Assert.Equal("version_conflict", conflict.Code);
        Assert.Equal(current.Version, conflict.CurrentVersion);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var unchanged = await dbContext.Reservations.AsNoTracking()
            .SingleAsync(reservation => reservation.ReservationId == current.ReservationId);
        Assert.Equal(current.Version, unchanged.Version);
        Assert.Equal(current.State, unchanged.State);
        Assert.Equal(current.SeatId, unchanged.SeatId);
        Assert.Equal(current.CustomerName, unchanged.CustomerName);
        Assert.Equal(current.PhoneNumber, unchanged.PhoneNumber);
        Assert.Equal(current.StartsAtUtc, unchanged.StartsAtUtc);
        Assert.Equal(current.EndsAtUtc, unchanged.EndsAtUtc);
        Assert.Equal(current.Note, unchanged.Note);
        Assert.Null(unchanged.CancelledAtUtc);
        Assert.Null(unchanged.SeatedAtUtc);
    }

    [Fact]
    public async Task CreateReservation_WithOverlappingSeat_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAsync(factory);

        var first = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations",
            new CreateReservationRequest(
                TestIds.OrganizationId,
                PlayerAccountId: null,
                SeatOneId,
                CustomerName: "First guest",
                PhoneNumber: null,
                StartsAtUtc: BookingDay.AddHours(16),
                DurationMinutes: 60,
                Source: ReservationSourceNames.Operator,
                Note: "first"));
        var second = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/reservations",
            new CreateReservationRequest(
                TestIds.OrganizationId,
                PlayerAccountId: null,
                SeatOneId,
                CustomerName: "Second guest",
                PhoneNumber: null,
                StartsAtUtc: BookingDay.AddHours(16).AddMinutes(30),
                DurationMinutes: 60,
                Source: ReservationSourceNames.Operator,
                Note: "overlap"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main",
            SortOrder = 10,
            CreatedAtUtc = BookingDay
        });
        dbContext.Seats.AddRange(
            new SeatEntity
            {
                SeatId = SeatOneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-01",
                SortOrder = 10,
                CreatedAtUtc = BookingDay
            },
            new SeatEntity
            {
                SeatId = SeatTwoId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-02",
                SortOrder = 20,
                CreatedAtUtc = BookingDay
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ReservationEntity> SeedReservationAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (!await dbContext.Organizations.AnyAsync(candidate => candidate.OrganizationId == TestIds.OrganizationId))
        {
            dbContext.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = TestIds.OrganizationId,
                Name = "Demo Org",
                CreatedAtUtc = BookingDay
            });
        }

        if (!await dbContext.Branches.AnyAsync(candidate => candidate.BranchId == TestIds.BranchId))
        {
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Demo Branch",
                CreatedAtUtc = BookingDay
            });
        }

        var reservation = new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatOneId,
            CustomerName = "Original guest",
            PhoneNumber = "+992900000001",
            StartsAtUtc = BookingDay.AddHours(16),
            EndsAtUtc = BookingDay.AddHours(17),
            State = ReservationStateNames.Pending,
            Source = ReservationSourceNames.Online,
            Note = "original note",
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            UpdatedByStaffUserId = TestIds.TechnicianStaffUserId,
            CreatedAtUtc = BookingDay,
            UpdatedAtUtc = BookingDay,
            CancelReason = string.Empty,
            Version = 1
        };
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();
        return reservation;
    }

    private static StartReservationSessionRequest StartRequest(int expectedVersion) =>
        new(
            TestIds.OrganizationId,
            expectedVersion,
            "standard-v1",
            "reservation-start-1",
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            BillingMode: "");

    private static ReservationSessionStartResult StartSuccess()
    {
        var sessionId = Guid.Parse("bbbbbbbb-3333-4333-8333-333333333333");
        return ReservationSessionStartResult.Ok(new StartReservationSessionResponse(
            new ReservationDto(
                Guid.Empty,
                TestIds.OrganizationId,
                TestIds.BranchId,
                PlayerAccountId: null,
                SeatOneId,
                "PC-01",
                "Main",
                "Original guest",
                "+992900000001",
                BookingDay.AddHours(16),
                BookingDay.AddHours(17),
                60,
                ReservationStateNames.Seated,
                ReservationSourceNames.Online,
                "original note",
                BookingDay,
                BookingDay,
                CancelledAtUtc: null,
                CancelReason: string.Empty,
                ReservationGroupId: null,
                Version: 2,
                StartedSessionId: sessionId),
            new SessionCommandResponse(
                "reservation-start-1",
                new SessionDto(
                    sessionId,
                    TestIds.OrganizationId,
                    TestIds.BranchId,
                    SeatOneId,
                    Guid.Parse("bbbbbbbb-4444-4444-8444-444444444444"),
                    SessionStateNames.Active,
                    "standard-v1",
                    BookingDay,
                    BookingDay.AddHours(1),
                    EndedAtUtc: null,
                    RemainingSeconds: 3600,
                    CurrentLease: null,
                    Version: 1),
                [])));
    }

    private static StaffContext StaffContextWithPermissions(params string[] permissions) =>
        new(
            TestIds.TechnicianStaffUserId,
            TestIds.OrganizationId,
            "Start Operator",
            new HashSet<Guid> { TestIds.BranchId },
            new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase));

    private static PlatformApiFactory CreateStartFactory(
        StubReservationSessionCoordinator coordinator,
        StaffContext staffContext) =>
        new(extraServices: services =>
        {
            services.RemoveAll<IReservationSessionCoordinator>();
            services.AddSingleton<IReservationSessionCoordinator>(coordinator);
            services.RemoveAll<IStaffTokenService>();
            services.AddSingleton<IStaffTokenService>(new StubStaffTokenService(staffContext));
        });

    private static HttpClient AuthorizedClient(PlatformApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "start-token");
        return client;
    }

    private sealed class StubReservationSessionCoordinator(ReservationSessionStartResult result)
        : IReservationSessionCoordinator
    {
        public ReservationSessionStartResult Result { get; } = result;
        public Guid? LastReservationId { get; private set; }
        public Guid? LastActorStaffUserId { get; private set; }
        public bool LastActorCanApproveComp { get; private set; }
        public StartReservationSessionRequest? LastRequest { get; private set; }

        public Task<ReservationSessionStartResult> StartAsync(
            Guid reservationId,
            Guid actorStaffUserId,
            bool actorCanApproveComp,
            StartReservationSessionRequest request,
            CancellationToken cancellationToken)
        {
            LastReservationId = reservationId;
            LastActorStaffUserId = actorStaffUserId;
            LastActorCanApproveComp = actorCanApproveComp;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubStaffTokenService(StaffContext context) : IStaffTokenService
    {
        public Task<StaffSignInResponse> IssueAsync(
            StaffUserEntity user,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffSignInResponse?> RefreshAsync(
            StaffRefreshTokenRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffContext?> ValidateAsync(
            string? bearerToken,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffContext?>(bearerToken == "start-token" ? context : null);
    }

    private sealed record ReservationConflictBody(string? Error, string? Code, int? CurrentVersion);
}
