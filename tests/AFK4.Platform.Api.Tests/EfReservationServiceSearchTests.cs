using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfReservationServiceSearchTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerOne = Guid.Parse("11111111-1111-4111-1111-111111111111");
    private static readonly Guid PlayerTwo = Guid.Parse("22222222-2222-4222-2222-222222222222");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-06-18T16:00:00Z");

    private static ReservationEntity Reservation(Guid id, Guid? playerId) => new()
    {
        ReservationId = id,
        OrganizationId = OrgId,
        BranchId = BranchId,
        PlayerAccountId = playerId,
        SeatId = SeatId,
        CustomerName = "Гость",
        StartsAtUtc = Start,
        EndsAtUtc = Start.AddHours(1),
        State = ReservationStateNames.Confirmed,
        Source = "operator",
        CreatedByStaffUserId = Guid.NewGuid(),
        CreatedAtUtc = Start.AddMinutes(-30),
        UpdatedAtUtc = Start.AddMinutes(-30)
    };

    [Fact]
    public async Task SearchAsync_FiltersByPlayerAccountId_ReturnsOnlyThatClientsReservations()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var onlyP1 = Guid.Parse("aaaa1111-1111-4111-1111-111111111111");

        await using (var db = new PlatformDbContext(options))
        {
            db.Zones.Add(new ZoneEntity { ZoneId = ZoneId, OrganizationId = OrgId, BranchId = BranchId, Name = "Зал A", SortOrder = 1, CreatedAtUtc = Start });
            db.Seats.Add(new SeatEntity { SeatId = SeatId, OrganizationId = OrgId, BranchId = BranchId, ZoneId = ZoneId, Name = "PC-01", SortOrder = 10, CreatedAtUtc = Start });
            db.Reservations.Add(Reservation(onlyP1, PlayerOne));
            db.Reservations.Add(Reservation(Guid.NewGuid(), PlayerTwo));
            db.Reservations.Add(Reservation(Guid.NewGuid(), null)); // walk-in без аккаунта
            await db.SaveChangesAsync();
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfReservationService(db, TimeProvider.System);
            var query = new ReservationSearchQuery(Start.AddHours(-1), Start.AddHours(2), State: null, Source: null, Limit: null, PlayerAccountId: PlayerOne);

            var result = await service.SearchAsync(OrgId, BranchId, query, CancellationToken.None);

            var reservation = Assert.Single(result.Reservations);
            Assert.Equal(onlyP1, reservation.ReservationId);
            Assert.Equal(PlayerOne, reservation.PlayerAccountId);
        }
    }
}
