using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Sessions;

/// <summary>
/// Журнал кассы без интернета (docs/roadmap/offline-front-desk.md): деньги ложатся теми же строками, что
/// при посадке онлайн, — в смену, где их взяли, со временем, когда их взяли, ровно в той сумме, — и
/// ни одна запись не применяется дважды.
/// </summary>
public sealed class OfflineJournalTests
{
    private static readonly string Route = $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/offline-journal";
    private static readonly DateTimeOffset Offline = DateTimeOffset.Parse("2026-10-27T10:00:00Z");

    [Fact]
    public async Task ACashGuestSeatedOffline_LandsInTheShiftWhereTheMoneyWasTaken_Once()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        var sessionId = Guid.NewGuid();
        var journal = new OfflineJournalUploadRequest(
        [
            Started("r1", sessionId, shiftId, minutes: 60, charge: 12000),
            new OfflineJournalRecordDto("r2", OfflineJournalKindNames.SessionExtended, sessionId, Offline.AddMinutes(50), shiftId,
                Minutes: 30, ChargeMinorUnits: 3000, CurrencyCode: "TJS"),
            new OfflineJournalRecordDto("r3", OfflineJournalKindNames.SessionEnded, sessionId, Offline.AddMinutes(85), shiftId)
        ]);

        var first = await UploadAsync(client, journal);
        var again = await UploadAsync(client, journal);

        Assert.All(first.Results, result => Assert.Equal(OfflineJournalStatusNames.Applied, result.Status));
        Assert.All(again.Results, result => Assert.Equal(OfflineJournalStatusNames.Duplicate, result.Status));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(row => row.SessionId == sessionId);
        Assert.Equal(SessionOriginNames.Offline, session.Origin);
        Assert.Equal(SessionStateNames.Ended, session.State);
        Assert.Equal(Offline.AddMinutes(85), session.EndedAtUtc);
        Assert.Equal(Offline.AddMinutes(90), session.EndsAtUtc);
        var payments = await db.Payments.Where(row => row.SessionId == sessionId).OrderBy(row => row.CreatedAtUtc).ToListAsync();
        Assert.Equal([12000L, 3000L], payments.Select(row => row.AmountMinorUnits));
        Assert.All(payments, payment => Assert.Equal(shiftId, payment.ShiftId));
        Assert.Equal(Offline, payments[0].CreatedAtUtc);
        Assert.Equal(-15000, await db.LedgerEntries.Where(row => row.SessionId == sessionId).SumAsync(row => row.AmountMinorUnits));

        // Касса сходится: ожидаемые наличные смены включают взятое без связи.
        var closed = await scope.ServiceProvider.GetRequiredService<EfShiftService>().CloseShiftAsync(
            shiftId, TestIds.TechnicianStaffUserId,
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 15000), "end of day", "close-1"), CancellationToken.None);
        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(0, closed.Response!.Difference?.MinorUnits ?? 0);
    }

    // Гость ещё играет: сессия становится серверной с тем же номером, и ПК получит серверную аренду.
    [Fact]
    public async Task ASessionStillRunning_BecomesALiveServerSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        var sessionId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        await UploadAsync(client, new OfflineJournalUploadRequest([Started("r1", sessionId, shiftId, 120, 24000) with { OccurredAtUtc = startedAt }]));

        await using var scope = factory.Services.CreateAsyncScope();
        var session = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Sessions.SingleAsync(row => row.SessionId == sessionId);
        Assert.Equal(SessionStateNames.Active, session.State);
        Assert.Equal(startedAt.AddMinutes(120), session.EndsAtUtc);
    }

    // Связь вернулась раньше журнала, и за ПК уже посадили следующего гостя онлайн: офлайн-сессия
    // окончена и место не занимает, а деньги за неё всё равно в кассе.
    [Fact]
    public async Task WhenTheNextGuestIsAlreadySeated_TheOfflineSessionIsClosed_AndItsMoneyStillCounts()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Sessions.Add(new SessionEntity
            {
                SessionId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
                SeatId = TestIds.SeatId, DeviceId = TestIds.DeviceId, CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                BillingMode = BillingModeNames.PrepaidCash, Origin = SessionOriginNames.Operator, State = SessionStateNames.Active,
                RequestedAtUtc = DateTimeOffset.UtcNow, StartedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var offlineId = Guid.NewGuid();
        await UploadAsync(client, new OfflineJournalUploadRequest([Started("r1", offlineId, shiftId, 120, 24000) with { OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20) }]));

        await using var verify = factory.Services.CreateAsyncScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(SessionStateNames.Ended, (await dbVerify.Sessions.SingleAsync(row => row.SessionId == offlineId)).State);
        Assert.Equal(24000, await dbVerify.Payments.Where(row => row.SessionId == offlineId).SumAsync(row => row.AmountMinorUnits));
    }

    // В закрытую смену задним числом не пишем: отказ с кодом, но запись остаётся у сервера.
    [Fact]
    public async Task MoneyForAClosedShift_IsRefused_ButKept()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var closed = await scope.ServiceProvider.GetRequiredService<EfShiftService>().CloseShiftAsync(
                shiftId, TestIds.TechnicianStaffUserId,
                new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "closed", "close-1"), CancellationToken.None);
            Assert.True(closed.Succeeded, closed.Error);
        }

        var result = Assert.Single((await UploadAsync(client, new OfflineJournalUploadRequest([Started("r1", Guid.NewGuid(), shiftId, 60, 12000)]))).Results);

        Assert.Equal(OfflineJournalStatusNames.Rejected, result.Status);
        Assert.Equal(OfflineJournalRejectionNames.ShiftClosed, result.Code);
        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await db.Payments.CountAsync());
        var kept = await db.OfflineJournalRecords.SingleAsync();
        Assert.Equal(OfflineJournalRejectionNames.ShiftClosed, kept.RejectionCode);
        Assert.Contains("12000", kept.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SomeoneWhoCannotSeatGuests_CannotUploadAJournal()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        var response = await client.PostAsJsonAsync(Route, new OfflineJournalUploadRequest([Started("r1", Guid.NewGuid(), Guid.NewGuid(), 60, 100)]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Товар без связи: те же строки, что онлайн-продажа, — чек, платёж наличными в смену, списание со
    // склада — с ценой, по которой взяли деньги, и временем продажи. Остаток в минус не останавливает: товар отдан.
    [Fact]
    public async Task GoodsSoldOffline_AreReceipted_PaidInCash_AndTakenOffStock()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        var cola = await SeedProductAsync(factory, "Cola", priceMinorUnits: 1500);

        var result = await UploadAsync(client, new OfflineJournalUploadRequest(
        [
            new OfflineJournalRecordDto("s1", OfflineJournalKindNames.Sale, Guid.Empty, Offline, shiftId,
                ChargeMinorUnits: 2400, CurrencyCode: "TJS", Lines: [new OfflineSaleLineDto(cola, 2, 1200)])
        ]));

        Assert.Equal(OfflineJournalStatusNames.Applied, Assert.Single(result.Results).Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sale = await db.PosSales.SingleAsync();
        Assert.Equal((PosSaleStateNames.Paid, 2400L, shiftId, Offline), (sale.State, sale.TotalMinorUnits, sale.ShiftId, sale.PaidAtUtc!.Value));
        var payment = await db.Payments.SingleAsync(row => row.PosSaleId == sale.PosSaleId);
        Assert.Equal((PaymentMethodNames.Cash, 2400L, shiftId), (payment.PaymentMethod, payment.AmountMinorUnits, payment.ShiftId));
        Assert.Equal(-2, (await db.StockMovements.SingleAsync(row => row.ProductId == cola)).QuantityDelta);
        Assert.Equal(sale.PosSaleId, (await db.Receipts.SingleAsync()).PosSaleId);
    }

    [Fact]
    public async Task ASaleWhoseLinesDoNotAddUpToTheMoneyTaken_IsRefused()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client);
        var cola = await SeedProductAsync(factory, "Cola", priceMinorUnits: 1500);

        var result = await UploadAsync(client, new OfflineJournalUploadRequest(
        [
            new OfflineJournalRecordDto("s1", OfflineJournalKindNames.Sale, Guid.Empty, Offline, shiftId,
                ChargeMinorUnits: 100, CurrencyCode: "TJS", Lines: [new OfflineSaleLineDto(cola, 2, 1200)])
        ]));

        Assert.Equal(OfflineJournalRejectionNames.Invalid, Assert.Single(result.Results).Code);
    }

    // Смену сдали без связи и открыли следующую: расхождение сервер считает по всем деньгам смены, включая
    // присланные в том же журнале, а деньги после пересменки ложатся в новую смену.
    [Fact]
    public async Task AShiftHandedOverOffline_ClosesAtThatTime_AndTheNextShiftTakesTheLaterMoney()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client, OrganizationRoleNames.Operator);
        var cola = await SeedProductAsync(factory, "Cola", priceMinorUnits: 1000);
        var nextShiftId = Guid.NewGuid();

        var result = await UploadAsync(client, new OfflineJournalUploadRequest(
        [
            new OfflineJournalRecordDto("s1", OfflineJournalKindNames.Sale, Guid.Empty, Offline, shiftId,
                ChargeMinorUnits: 1000, CurrencyCode: "TJS", Lines: [new OfflineSaleLineDto(cola, 1, 1000)]),
            new OfflineJournalRecordDto("c1", OfflineJournalKindNames.ShiftClosed, Guid.Empty, Offline.AddHours(1), shiftId,
                CashMinorUnits: 1000, Note: "handover"),
            new OfflineJournalRecordDto("o1", OfflineJournalKindNames.ShiftOpened, Guid.Empty, Offline.AddHours(1).AddMinutes(1), nextShiftId,
                CurrencyCode: "TJS", CashMinorUnits: 1000),
            new OfflineJournalRecordDto("s2", OfflineJournalKindNames.Sale, Guid.Empty, Offline.AddHours(2), nextShiftId,
                ChargeMinorUnits: 1000, CurrencyCode: "TJS", Lines: [new OfflineSaleLineDto(cola, 1, 1000)])
        ]));

        Assert.All(result.Results, row => Assert.Equal(OfflineJournalStatusNames.Applied, row.Status));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var closed = await db.Shifts.SingleAsync(row => row.ShiftId == shiftId);
        Assert.Equal((ShiftStateNames.Closed, Offline.AddHours(1), 1000L, 0L), (closed.State, closed.ClosedAtUtc!.Value, closed.ExpectedCashMinorUnits, closed.DifferenceMinorUnits));
        Assert.Null(closed.SignOffReason);
        var next = await db.Shifts.SingleAsync(row => row.ShiftId == nextShiftId);
        Assert.Equal((ShiftStateNames.Open, 1000L), (next.State, next.StartingCashMinorUnits));
        Assert.Equal(nextShiftId, (await db.Payments.SingleAsync(row => row.CreatedAtUtc == Offline.AddHours(2))).ShiftId);
    }

    // Расхождение сверх допуска без связи подписать некому: смена всё равно закрывается — иначе встала бы
    // вся следующая смена, — но с пометкой «без подписи», а владелец получает тревогу как обычно.
    [Fact]
    public async Task AShortfallOverToleranceOffline_ClosesTheShift_MarkedUnsigned()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client, OrganizationRoleNames.Operator);
        var cola = await SeedProductAsync(factory, "Cola", priceMinorUnits: 100000);

        var result = await UploadAsync(client, new OfflineJournalUploadRequest(
        [
            new OfflineJournalRecordDto("s1", OfflineJournalKindNames.Sale, Guid.Empty, Offline, shiftId,
                ChargeMinorUnits: 100000, CurrencyCode: "TJS", Lines: [new OfflineSaleLineDto(cola, 1, 100000)]),
            new OfflineJournalRecordDto("c1", OfflineJournalKindNames.ShiftClosed, Guid.Empty, Offline.AddHours(1), shiftId, CashMinorUnits: 0)
        ]));

        Assert.All(result.Results, row => Assert.Equal(OfflineJournalStatusNames.Applied, row.Status));
        await using var scope = factory.Services.CreateAsyncScope();
        var closed = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Shifts.SingleAsync(row => row.ShiftId == shiftId);
        Assert.Equal((ShiftStateNames.Closed, -100000L), (closed.State, closed.DifferenceMinorUnits));
        Assert.Equal(OfflineCashRecords.UnsignedOfflineCloseReason, closed.SignOffReason);
    }

    // Оператор сдаёт только свою смену: чужую без связи не закроет, как и при связи.
    [Fact]
    public async Task AnOperatorCannotCloseSomeoneElsesShiftOffline()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        using var client = factory.CreateClient();
        var shiftId = await SeedAsync(factory, client, OrganizationRoleNames.Operator, openedBy: Guid.NewGuid());

        var result = await UploadAsync(client, new OfflineJournalUploadRequest(
        [
            new OfflineJournalRecordDto("c1", OfflineJournalKindNames.ShiftClosed, Guid.Empty, Offline, shiftId, CashMinorUnits: 0)
        ]));

        Assert.Equal(OfflineJournalRejectionNames.NotAllowed, Assert.Single(result.Results).Code);
    }

    private static OfflineJournalRecordDto Started(string id, Guid sessionId, Guid shiftId, int minutes, long charge) =>
        new(id, OfflineJournalKindNames.SessionStarted, sessionId, Offline, shiftId,
            SeatId: TestIds.SeatId, DeviceId: TestIds.DeviceId, TariffRuleVersionId: Guid.NewGuid().ToString("D"),
            Minutes: minutes, ChargeMinorUnits: charge, CurrencyCode: "TJS");

    private static async Task<OfflineJournalUploadResponse> UploadAsync(HttpClient client, OfflineJournalUploadRequest request)
    {
        var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OfflineJournalUploadResponse>())!;
    }

    /// <returns>Смена, открытая до обрыва связи.</returns>
    private static async Task<Guid> SeedAsync(
        PlatformApiFactory factory, HttpClient client, string role = OrganizationRoleNames.OrganizationOwner, Guid? openedBy = null)
    {
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, role);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var at = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        db.Zones.Add(new ZoneEntity { ZoneId = TestIds.ZoneId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Name = "Hall", SortOrder = 1, CreatedAtUtc = at });
        db.Seats.Add(new SeatEntity { SeatId = TestIds.SeatId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, ZoneId = TestIds.ZoneId, Name = "PC-01", SortOrder = 1, CreatedAtUtc = at });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = TestIds.DeviceId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
            MachineName = "PC-01", AgentVersion = "0.1.0", ShellVersion = "0.1.0", EnrolledAtUtc = at, IsLocked = true
        });
        await db.SaveChangesAsync();

        var opened = await scope.ServiceProvider.GetRequiredService<EfShiftService>().OpenShiftAsync(
            TestIds.BranchId, openedBy ?? TestIds.TechnicianStaffUserId,
            new OpenShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 0), "front", "open-1"), CancellationToken.None);
        Assert.True(opened.Succeeded, opened.Error);
        return opened.Response!.ShiftId;
    }

    private static async Task<Guid> SeedProductAsync(PlatformApiFactory factory, string name, long priceMinorUnits)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var at = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var category = new PosProductCategoryEntity
        {
            CategoryId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Name = "Drinks", IsActive = true, CreatedAtUtc = at
        };
        var product = new PosProductEntity
        {
            ProductId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, CategoryId = category.CategoryId,
            Name = name, Sku = $"SKU-{Guid.NewGuid():N}", CurrencyCode = "TJS", PriceMinorUnits = priceMinorUnits,
            TrackStock = true, IsActive = true, CreatedAtUtc = at
        };
        db.PosProductCategories.Add(category);
        db.PosProducts.Add(product);
        await db.SaveChangesAsync();
        return product.ProductId;
    }
}
