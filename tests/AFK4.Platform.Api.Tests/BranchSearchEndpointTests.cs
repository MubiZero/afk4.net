using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shop;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Поиск по филиалу — то, что стоит за палитрой оператора. Смысл проверок один: палитра находит
/// то, что человек набирает вслух («PC-12», имя гостя, номер чека), и не находит того, чего ему
/// и так не видно.
/// </summary>
public sealed class BranchSearchEndpointTests
{
    private static readonly Guid ZoneId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b001");
    private static readonly Guid SeatAId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b002");
    private static readonly Guid SeatBId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b003");
    private static readonly Guid DeviceAId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b004");
    private static readonly Guid PlayerId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b005");
    private static readonly Guid ReservationId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b006");
    private static readonly Guid PastReservationId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b007");
    private static readonly Guid ReceiptId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b008");
    private static readonly Guid OtherBranchSeatId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b009");
    private static readonly Guid OpenOrderId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b010");
    private static readonly Guid DeliveredOrderId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b011");
    private static readonly Guid OtherBranchOrderId = Guid.Parse("3f2b2a41-1f74-4a2a-9a27-59d6d4a2b012");

    private static readonly DateTimeOffset Seeded = DateTimeOffset.Parse("2026-05-25T09:00:00Z");

    [Fact]
    public async Task Search_FindsTheSeatByTheNameOnTheWall()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "PC-12");

        var seat = Assert.Single(results, result => result.Kind == BranchSearchKindNames.Seat);
        Assert.Equal("PC-12", seat.Title);
        Assert.Equal(SeatAId, seat.Id);
        Assert.Equal("Main Hall", seat.Subtitle);
    }

    // В заявке из поддержки приходит имя машины в сети, а не надпись на корпусе.
    [Fact]
    public async Task Search_FindsTheSeatByTheMachineNameToo()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "VM-GAME-12");

        var seat = Assert.Single(results, result => result.Kind == BranchSearchKindNames.Seat);
        Assert.Equal(SeatAId, seat.Id);
    }

    [Fact]
    public async Task Search_FindsThePlayerByNameAndByPhone()
    {
        var byName = await SearchAsAsync(OrganizationRoleNames.Operator, "Карим");
        var byPhone = await SearchAsAsync(OrganizationRoleNames.Operator, "9001234");

        var named = Assert.Single(byName, result => result.Kind == BranchSearchKindNames.Player);
        var called = Assert.Single(byPhone, result => result.Kind == BranchSearchKindNames.Player);
        Assert.Equal(PlayerId, named.Id);
        Assert.Equal(PlayerId, called.Id);
        Assert.Equal("+992 90 0123456", named.Subtitle);
    }

    // Гость приходит к стойке за ближайшей бронью, а не за прошлогодней: сначала предстоящие.
    [Fact]
    public async Task Search_PutsTheUpcomingReservationAheadOfTheFinishedOne()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "Далер");

        var reservations = results.Where(result => result.Kind == BranchSearchKindNames.Reservation).ToList();
        Assert.Equal(2, reservations.Count);
        Assert.Equal(ReservationId, reservations[0].Id);
        Assert.Equal(PastReservationId, reservations[1].Id);
        Assert.NotNull(reservations[0].OccursAtUtc);
    }

    // С чеком приходят через неделю, и в руках у человека именно номер — вместе с суммой, чтобы
    // из двух похожих строк он узнал свою.
    [Fact]
    public async Task Search_FindsTheReceiptByItsNumberWithTheAmount()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "POS-20260525-0007");

        var receipt = Assert.Single(results, result => result.Kind == BranchSearchKindNames.Receipt);
        Assert.Equal(ReceiptId, receipt.Id);
        Assert.Equal(4500, receipt.AmountMinorUnits);
        Assert.Equal("TJS", receipt.CurrencyCode);
    }

    // Ради этого и вводилась область видимости: техник видит зал, но не клиентов, брони и чеки —
    // иначе палитра стала бы обходом прав.
    [Fact]
    public async Task Search_ForATechnician_ReturnsSeatsOnly()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Technician, "PC-12");
        var people = await SearchAsAsync(OrganizationRoleNames.Technician, "Карим");

        Assert.All(results, result => Assert.Equal(BranchSearchKindNames.Seat, result.Kind));
        Assert.Empty(people);
    }

    // И обратная сторона: бухгалтер видит клиентов, брони и чеки, но карты зала у него нет.
    [Fact]
    public async Task Search_ForAnAccountant_ReturnsNoSeats()
    {
        var seats = await SearchAsAsync(OrganizationRoleNames.Accountant, "PC-12");
        var receipts = await SearchAsAsync(OrganizationRoleNames.Accountant, "POS-20260525-0007");

        Assert.Empty(seats);
        Assert.Single(receipts, result => result.Kind == BranchSearchKindNames.Receipt);
    }

    // «PC-12» — имя места, а не номер: если бы цифры из него шли в поиск по телефону, каждый
    // набор имени ПК вытаскивал бы всех, у кого в номере есть «12».
    [Fact]
    public async Task Search_ByASeatName_DoesNotDragInEveryPhoneWithThoseDigits()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "PC-12");

        Assert.DoesNotContain(results, result => result.Kind == BranchSearchKindNames.Player);
    }

    // Заказ называют вслух по номеру чека, по гостю или по месту — и каждое из трёх находит его.
    [Fact]
    public async Task Search_FindsTheOrderByItsNumber()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "POS-20260525-0011");

        var order = Assert.Single(results, result => result.Kind == BranchSearchKindNames.Order);
        Assert.Equal(OpenOrderId, order.Id);
        Assert.Equal("PC-12", order.Title);
        Assert.Equal("Карим Рахимов", order.Subtitle);
        Assert.Equal("POS-20260525-0011", order.Number);
        Assert.Equal(ShopOrderStatusNames.Accepted, order.Status);
        Assert.Equal(3200, order.AmountMinorUnits);
        Assert.Equal("TJS", order.CurrencyCode);
        Assert.NotNull(order.OccursAtUtc);
    }

    // Заказ, который ещё несут, важнее вчерашнего: с ним и подходят к стойке прямо сейчас.
    [Fact]
    public async Task Search_FindsTheOrdersByTheGuest_TheOpenOneFirst()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "Карим");

        var orders = results.Where(result => result.Kind == BranchSearchKindNames.Order).ToList();
        Assert.Equal([OpenOrderId, DeliveredOrderId], orders.Select(order => order.Id));
        Assert.Equal(ShopOrderStatusNames.Delivered, orders[1].Status);
    }

    [Fact]
    public async Task Search_FindsTheOrderByTheSeat()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "PC-13");

        var order = Assert.Single(results, result => result.Kind == BranchSearchKindNames.Order);
        Assert.Equal(DeliveredOrderId, order.Id);
    }

    // Заказы видит тот, кто их выдаёт: у бухгалтера и техника ленты заказов нет — нет и поиска.
    [Fact]
    public async Task Search_WithoutTheOrderFeed_FindsNoOrders()
    {
        var accountant = await SearchAsAsync(OrganizationRoleNames.Accountant, "Карим");
        var technician = await SearchAsAsync(OrganizationRoleNames.Technician, "PC-12");

        Assert.DoesNotContain(accountant, result => result.Kind == BranchSearchKindNames.Order);
        Assert.DoesNotContain(technician, result => result.Kind == BranchSearchKindNames.Order);
    }

    [Fact]
    public async Task Search_DoesNotFindAnOrderOfAnotherBranch()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "POS-20260525-0099");

        Assert.DoesNotContain(results, result => result.Kind == BranchSearchKindNames.Order);
    }

    // Одна буква совпала бы с половиной клубной базы на каждом нажатии.
    [Fact]
    public async Task Search_WithASingleCharacter_ReturnsNothing()
    {
        Assert.Empty(await SearchAsAsync(OrganizationRoleNames.Operator, "P"));
    }

    [Fact]
    public async Task Search_DoesNotCrossBranchBoundaries()
    {
        var results = await SearchAsAsync(OrganizationRoleNames.Operator, "PC-99");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WithoutAuthentication_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/search?query=PC-12");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<IReadOnlyList<BranchSearchResultDto>> SearchAsAsync(string roleName, string query)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, roleName);
        await SeedAsync(factory);

        var response = await client.GetAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/search?query={Uri.EscapeDataString(query)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<List<BranchSearchResultDto>>() ?? [];
    }

    private static async Task SeedAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Seeded
        });
        dbContext.Seats.AddRange(
            new SeatEntity
            {
                SeatId = SeatAId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-12",
                SortOrder = 1,
                CreatedAtUtc = Seeded
            },
            new SeatEntity
            {
                SeatId = SeatBId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-13",
                SortOrder = 2,
                CreatedAtUtc = Seeded
            },
            new SeatEntity
            {
                SeatId = OtherBranchSeatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.OtherBranchId,
                ZoneId = ZoneId,
                Name = "PC-99",
                SortOrder = 1,
                CreatedAtUtc = Seeded
            });
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = DeviceAId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = "VM-GAME-12",
            DisplayName = "PC-12",
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = DeviceEnrollmentStateNames.Approved,
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            EnrolledAtUtc = Seeded,
            IsOnline = true
        });
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatAId,
            DeviceId = DeviceAId,
            AttachedAtUtc = Seeded
        });
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Карим Рахимов",
            PhoneNumber = "+992 90 0123456",
            IsActive = true,
            CreatedAtUtc = Seeded
        });
        dbContext.Reservations.AddRange(
            new ReservationEntity
            {
                ReservationId = ReservationId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                CustomerName = "Далер Назаров",
                PhoneNumber = "+992 90 7654321",
                StartsAtUtc = now.AddHours(3),
                EndsAtUtc = now.AddHours(5),
                State = ReservationStateNames.Confirmed,
                Source = ReservationSourceNames.Operator,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                CreatedAtUtc = Seeded,
                UpdatedAtUtc = Seeded
            },
            new ReservationEntity
            {
                ReservationId = PastReservationId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                CustomerName = "Далер Назаров",
                PhoneNumber = "+992 90 7654321",
                StartsAtUtc = now.AddDays(-7),
                EndsAtUtc = now.AddDays(-7).AddHours(2),
                State = ReservationStateNames.Seated,
                Source = ReservationSourceNames.Operator,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                CreatedAtUtc = Seeded,
                UpdatedAtUtc = Seeded
            });
        dbContext.Receipts.Add(new ReceiptEntity
        {
            ReceiptId = ReceiptId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PosSaleId = Guid.NewGuid(),
            ReceiptNumber = "POS-20260525-0007",
            ReceiptType = "sale",
            CurrencyCode = "TJS",
            TotalMinorUnits = 4500,
            CreatedAtUtc = Seeded
        });
        SeedOrder(dbContext, OpenOrderId, TestIds.BranchId, SeatAId, ShopOrderStatusNames.Accepted, now.AddMinutes(-10), "POS-20260525-0011");
        SeedOrder(dbContext, DeliveredOrderId, TestIds.BranchId, SeatBId, ShopOrderStatusNames.Delivered, now.AddDays(-2), "POS-20260523-0003");
        SeedOrder(dbContext, OtherBranchOrderId, TestIds.OtherBranchId, OtherBranchSeatId, ShopOrderStatusNames.Placed, now.AddMinutes(-5), "POS-20260525-0099");

        await dbContext.SaveChangesAsync();
    }

    // Заказ из приложения игрока оплачен кошельком сразу при оформлении — у него всегда есть
    // продажа и чек продажи, а номер этого чека и есть номер заказа.
    private static void SeedOrder(
        PlatformDbContext dbContext,
        Guid orderId,
        Guid branchId,
        Guid seatId,
        string status,
        DateTimeOffset placedAtUtc,
        string receiptNumber)
    {
        var saleId = Guid.NewGuid();
        dbContext.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId,
            ShiftId = Guid.NewGuid(),
            PlayerAccountId = PlayerId,
            State = "paid",
            CurrencyCode = "TJS",
            TotalMinorUnits = 3200,
            CreatedAtUtc = placedAtUtc,
            PaidAtUtc = placedAtUtc
        });
        dbContext.Receipts.Add(new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId,
            PosSaleId = saleId,
            ReceiptNumber = receiptNumber,
            ReceiptType = "sale",
            CurrencyCode = "TJS",
            TotalMinorUnits = 3200,
            CreatedAtUtc = placedAtUtc
        });
        dbContext.ShopOrders.Add(new ShopOrderEntity
        {
            ShopOrderId = orderId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId,
            PlayerAccountId = PlayerId,
            SessionId = Guid.NewGuid(),
            SeatId = seatId,
            Status = status,
            TotalMinorUnits = 3200,
            CurrencyCode = "TJS",
            WalletLedgerEntryId = Guid.NewGuid(),
            PosSaleId = saleId,
            PlacedAtUtc = placedAtUtc,
            DeliveredAtUtc = status == ShopOrderStatusNames.Delivered ? placedAtUtc.AddMinutes(15) : null,
            Version = 1
        });
    }
}
