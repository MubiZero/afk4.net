using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// ПК без киоска выходит из зала: место за ним нельзя ни забронировать, ни посадить по брони, и в
/// вместимость зала оно не входит. Иначе бронь обещала бы игроку машину, на которую сесть нельзя.
/// </summary>
public sealed class ReservationKioskAbsentTests
{
    private static readonly Guid OrganizationId = Guid.Parse("81111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("82222222-2222-4222-8222-222222222222");
    private static readonly Guid ZoneId = Guid.Parse("83333333-3333-4333-8333-333333333333");
    private static readonly Guid Actor = Guid.Parse("d4444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T14:00:00Z");
    private static readonly DateTimeOffset Start = Now.AddHours(4);

    private readonly Guid playingSeat = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private readonly Guid noKioskSeat = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    private async Task<DbContextOptions<PlatformDbContext>> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new PlatformDbContext(options);
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId, OrganizationId = OrganizationId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Now
        });
        foreach (var (seatId, name, kioskAbsent) in new[] { (playingSeat, "PC-01", false), (noKioskSeat, "PC-02", true) })
        {
            var deviceId = Guid.NewGuid();
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId, OrganizationId = OrganizationId, BranchId = BranchId, ZoneId = ZoneId,
                Name = name, SortOrder = 1, CreatedAtUtc = Now
            });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = deviceId, OrganizationId = OrganizationId, BranchId = BranchId, MachineName = name,
                Role = DeviceRoleNames.GamingPc, EnrollmentState = DeviceEnrollmentStateNames.Approved,
                KioskAbsentSinceUtc = kioskAbsent ? Now.AddDays(-1) : null
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(), OrganizationId = OrganizationId, BranchId = BranchId,
                SeatId = seatId, DeviceId = deviceId, AttachedAtUtc = Now.AddDays(-2)
            });
        }

        await db.SaveChangesAsync();
        return options;
    }

    private static CreateReservationRequest Create(Guid seatId) => new(
        OrganizationId, PlayerAccountId: null, seatId, "Гость", PhoneNumber: null, Start, 60, ReservationSourceNames.Operator, Note: null);

    [Fact]
    public async Task ASeatBehindAPcWithoutKiosk_CannotBeBooked_ButTheOtherOneCan()
    {
        await using var db = new PlatformDbContext(await SeedAsync());
        var service = new EfReservationService(db, new FixedTimeProvider(Now));

        var refused = await service.CreateAsync(BranchId, Actor, Create(noKioskSeat), CancellationToken.None);
        var accepted = await service.CreateAsync(BranchId, Actor, Create(playingSeat), CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.True(refused.Conflict);
        Assert.Equal(DeviceCommandErrorCodeNames.KioskRemoved, refused.Code);
        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task TheFreeSeatList_LeavesOutAPcWithoutKiosk()
    {
        await using var db = new PlatformDbContext(await SeedAsync());
        var service = new EfReservationService(db, new FixedTimeProvider(Now));

        var free = await service.FindFreeSeatsAsync(OrganizationId, BranchId, Start, Start.AddHours(1), null, CancellationToken.None);

        Assert.Equal([playingSeat], free.FreeSeatIds);
    }

    [Fact]
    public async Task ABookingMovedOntoAPcWithoutKiosk_IsRefused()
    {
        await using var db = new PlatformDbContext(await SeedAsync());
        var service = new EfReservationService(db, new FixedTimeProvider(Now));
        var booked = (await service.CreateAsync(BranchId, Actor, Create(playingSeat), CancellationToken.None)).Response!;

        var moved = await service.UpdateAsync(
            booked.ReservationId,
            Actor,
            new UpdateReservationRequest(OrganizationId, null, noKioskSeat, null, null, null, null, null, null, booked.Version),
            CancellationToken.None);

        Assert.False(moved.Succeeded);
        Assert.Equal(DeviceCommandErrorCodeNames.KioskRemoved, moved.Code);
    }

    [Fact]
    public async Task ABookingAlreadyMadeOnASeatThatLaterLostItsKiosk_CannotBeSeated()
    {
        var options = await SeedAsync();
        await using var db = new PlatformDbContext(options);
        var service = new EfReservationService(db, new FixedTimeProvider(Now));
        var booked = (await service.CreateAsync(BranchId, Actor, Create(playingSeat), CancellationToken.None)).Response!;
        var device = await db.DeviceSeatAssignments.Where(assignment => assignment.SeatId == playingSeat)
            .Join(db.Devices, assignment => assignment.DeviceId, candidate => candidate.DeviceId, (_, candidate) => candidate)
            .SingleAsync();
        device.KioskAbsentSinceUtc = Now;
        await db.SaveChangesAsync();

        var seated = await service.SeatAsync(booked.ReservationId, Actor, new SeatReservationRequest(OrganizationId, booked.Version), CancellationToken.None);

        Assert.False(seated.Succeeded);
        Assert.Equal(DeviceCommandErrorCodeNames.KioskRemoved, seated.Code);
    }

    [Fact]
    public async Task ThePcWithoutKiosk_IsNotPartOfTheHallCapacity()
    {
        await using var db = new PlatformDbContext(await SeedAsync());

        var bookable = await BranchCapacity.LoadBookableSeatIdsAsync(db, OrganizationId, BranchId, CancellationToken.None);

        Assert.Equal([playingSeat], bookable);
    }
}
