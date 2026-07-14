using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfReservationGroupServiceTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatA = Guid.Parse("a1111111-1111-4111-1111-111111111111");
    private static readonly Guid SeatB = Guid.Parse("b2222222-2222-4222-2222-222222222222");
    private static readonly Guid SeatC = Guid.Parse("c3333333-3333-4333-3333-333333333333");
    private static readonly Guid Actor = Guid.Parse("d4444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T14:00:00Z");

    private static DbContextOptions<PlatformDbContext> NewOptions() =>
        new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static async Task SeedAsync(DbContextOptions<PlatformDbContext> options, bool sessionOnSeatC)
    {
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
        foreach (var (seatId, order) in new[] { (SeatA, 10), (SeatB, 20), (SeatC, 30) })
        {
            db.Seats.Add(new SeatEntity { SeatId = seatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = $"PC-{order}", SortOrder = order, CreatedAtUtc = Start });
        }
        if (sessionOnSeatC)
        {
            // Открытая сессия на SeatC блокирует его под бронь в любом будущем окне.
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, SeatId = SeatC,
                DeviceId = Guid.NewGuid(), CreatedByStaffUserId = Actor, PlayerKind = "guest",
                TariffRuleVersionId = "guest", State = SessionStateNames.Active,
                RequestedAtUtc = Start.AddMinutes(-30), StartedAtUtc = Start.AddMinutes(-30),
                EndsAtUtc = null, EndedAtUtc = null, UpdatedAtUtc = Start.AddMinutes(-30)
            });
        }
        await db.SaveChangesAsync();
    }

    private static CreateReservationGroupRequest Request(params Guid[] seatIds) => new(
        OrganizationId: OrgId,
        PlayerAccountId: null,
        SeatIds: seatIds,
        CustomerName: "Группа гостей",
        PhoneNumber: "+992900000009",
        StartsAtUtc: Start,
        DurationMinutes: 60,
        Source: "operator",
        Note: "массовая бронь");

    [Fact]
    public async Task CreateGroupAsync_CreatesOneReservationPerSeat_SharingGroupId()
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: false);

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var result = await service.CreateGroupAsync(BranchId, Actor, Request(SeatA, SeatB), CancellationToken.None);

        Assert.Equal(ReservationGroupStatus.Created, result.Status);
        Assert.NotNull(result.Result!.ReservationGroupId);
        Assert.Equal(2, result.Result.Reservations.Count);
        Assert.Empty(result.Result.Conflicts);
        // Both rows carry the same group id.
        Assert.All(result.Result.Reservations, r => Assert.Equal(result.Result.ReservationGroupId, r.ReservationGroupId));
        Assert.Equal(new[] { SeatA, SeatB }.OrderBy(x => x), result.Result.Reservations.Select(r => r.SeatId!.Value).OrderBy(x => x));
        Assert.Equal(2, await db.Reservations.CountAsync());
    }

    [Fact]
    public async Task CreateGroupAsync_RejectsWholeGroup_WhenAnySeatHasBlockingSession()
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: true);

        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);
        var result = await service.CreateGroupAsync(BranchId, Actor, Request(SeatA, SeatC), CancellationToken.None);

        Assert.Equal(ReservationGroupStatus.Conflict, result.Status);
        Assert.Null(result.Result!.ReservationGroupId);
        var conflict = Assert.Single(result.Result.Conflicts);
        Assert.Equal(SeatC, conflict.SeatId);
        // All-or-nothing: nothing was written, not even the free SeatA.
        Assert.Equal(0, await db.Reservations.CountAsync());
    }

    [Fact]
    public async Task ConfirmAsync_WhenTwoContextsLoadedSameVersion_ReturnsAuthoritativeConflictForLoser()
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: false);
        var reservationId = await SeedPendingReservationAsync(options);

        await using var winnerDb = new PlatformDbContext(options);
        await using var loserDb = new PlatformDbContext(options);
        await winnerDb.Reservations.SingleAsync(reservation => reservation.ReservationId == reservationId);
        await loserDb.Reservations.SingleAsync(reservation => reservation.ReservationId == reservationId);
        var winner = new EfReservationService(winnerDb, TimeProvider.System);
        var loser = new EfReservationService(loserDb, TimeProvider.System);
        var request = new ConfirmReservationRequest(OrgId, ExpectedVersion: 1);

        var won = await winner.ConfirmAsync(reservationId, Actor, request, CancellationToken.None);
        var lost = await loser.ConfirmAsync(reservationId, Actor, request, CancellationToken.None);

        Assert.True(won.Succeeded);
        Assert.Equal(2, won.Response!.Version);
        Assert.True(lost.Conflict);
        Assert.Equal("version_conflict", lost.Code);
        Assert.Equal(2, lost.CurrentVersion);
        await using var verificationDb = new PlatformDbContext(options);
        var persisted = await verificationDb.Reservations.AsNoTracking()
            .SingleAsync(reservation => reservation.ReservationId == reservationId);
        Assert.Equal(ReservationStateNames.Confirmed, persisted.State);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task ConfirmAsync_WhenAlreadyConfirmed_IsIdempotentAtCurrentVersionButRejectsOldVersion()
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: false);
        var reservationId = await SeedPendingReservationAsync(options);
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, TimeProvider.System);

        var first = await service.ConfirmAsync(
            reservationId,
            Actor,
            new ConfirmReservationRequest(OrgId, ExpectedVersion: 1),
            CancellationToken.None);
        var replay = await service.ConfirmAsync(
            reservationId,
            Actor,
            new ConfirmReservationRequest(OrgId, ExpectedVersion: 2),
            CancellationToken.None);
        var stale = await service.ConfirmAsync(
            reservationId,
            Actor,
            new ConfirmReservationRequest(OrgId, ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.Equal(2, first.Response!.Version);
        Assert.True(replay.Succeeded);
        Assert.Equal(2, replay.Response!.Version);
        Assert.True(stale.Conflict);
        Assert.Equal("version_conflict", stale.Code);
        Assert.Equal(2, stale.CurrentVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateAsync_WhenPatchIsEmptyOrExplicitlyIdentical_IsNoOpWithoutSavingOrChangingMetadata(
        bool explicitlyIdentical)
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: false);
        var reservationId = await SeedPendingReservationAsync(options);
        await using var db = new PlatformDbContext(options);
        var saveCalls = 0;
        db.SavingChanges += (_, _) => saveCalls++;
        var service = new EfReservationService(db, TimeProvider.System);
        var otherActor = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");

        var result = await service.UpdateAsync(
            reservationId,
            otherActor,
            new UpdateReservationRequest(
                OrgId,
                PlayerAccountId: null,
                SeatId: explicitlyIdentical ? SeatA : null,
                CustomerName: explicitlyIdentical ? "Concurrent guest" : null,
                PhoneNumber: explicitlyIdentical ? string.Empty : null,
                StartsAtUtc: explicitlyIdentical ? Start : null,
                DurationMinutes: explicitlyIdentical ? 60 : null,
                Source: explicitlyIdentical ? ReservationSourceNames.Online : null,
                Note: explicitlyIdentical ? string.Empty : null,
                ExpectedVersion: 1),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Response!.Version);
        Assert.Equal(Start.AddHours(-1), result.Response.UpdatedAtUtc);
        var tracked = await db.Reservations.SingleAsync(reservation => reservation.ReservationId == reservationId);
        Assert.Equal(Actor, tracked.UpdatedByStaffUserId);
        Assert.Equal(Start.AddHours(-1), tracked.UpdatedAtUtc);
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
        Assert.Equal(0, saveCalls);
    }

    [Fact]
    public async Task SeatAsync_WhenAlreadySeated_IsNoOpAtCurrentVersionButOldVersionConflicts()
    {
        var options = NewOptions();
        await SeedAsync(options, sessionOnSeatC: false);
        var reservationId = await SeedPendingReservationAsync(options);
        var seatedAt = Start.AddMinutes(-15);
        await using (var seedDb = new PlatformDbContext(options))
        {
            var seeded = await seedDb.Reservations.SingleAsync(reservation => reservation.ReservationId == reservationId);
            seeded.State = ReservationStateNames.Seated;
            seeded.SeatedAtUtc = seatedAt;
            await seedDb.SaveChangesAsync();
        }

        await using var db = new PlatformDbContext(options);
        var saveCalls = 0;
        db.SavingChanges += (_, _) => saveCalls++;
        var service = new EfReservationService(db, TimeProvider.System);
        var otherActor = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");

        var replay = await service.SeatAsync(
            reservationId,
            otherActor,
            new SeatReservationRequest(OrgId, ExpectedVersion: 1),
            CancellationToken.None);
        var stale = await service.SeatAsync(
            reservationId,
            otherActor,
            new SeatReservationRequest(OrgId, ExpectedVersion: 0),
            CancellationToken.None);

        Assert.True(replay.Succeeded);
        Assert.Equal(1, replay.Response!.Version);
        Assert.True(stale.Conflict);
        Assert.Equal("version_conflict", stale.Code);
        Assert.Equal(1, stale.CurrentVersion);
        var tracked = await db.Reservations.SingleAsync(reservation => reservation.ReservationId == reservationId);
        Assert.Equal(Actor, tracked.UpdatedByStaffUserId);
        Assert.Equal(Start.AddHours(-1), tracked.UpdatedAtUtc);
        Assert.Equal(seatedAt, tracked.SeatedAtUtc);
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
        Assert.Equal(0, saveCalls);
    }

    private static async Task<Guid> SeedPendingReservationAsync(DbContextOptions<PlatformDbContext> options)
    {
        await using var db = new PlatformDbContext(options);
        var reservation = new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = OrgId,
            BranchId = BranchId,
            SeatId = SeatA,
            CustomerName = "Concurrent guest",
            StartsAtUtc = Start,
            EndsAtUtc = Start.AddHours(1),
            State = ReservationStateNames.Pending,
            Source = ReservationSourceNames.Online,
            Note = string.Empty,
            CancelReason = string.Empty,
            CreatedByStaffUserId = Actor,
            UpdatedByStaffUserId = Actor,
            CreatedAtUtc = Start.AddHours(-1),
            UpdatedAtUtc = Start.AddHours(-1),
            Version = 1
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.ReservationId;
    }
}
