using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Сводка дашборда — первый экран смены: по ней оператор понимает, сколько денег сделал зал,
/// сколько мест занято и что требует внимания. Endpoint-тесты проверяли счастливый случай на
/// одном наборе данных; здесь проверяется сам расчёт — пустой зал, возвраты, переполненная
/// очередь и деление на ноль.
/// </summary>
public sealed class EfOperatorDashboardServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-03T18:00:00Z");
    private static readonly DateTimeOffset DayStart = DateTimeOffset.Parse("2026-09-03T00:00:00Z");
    private static readonly Guid StaffId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid PlayerId = Guid.Parse("88888888-8888-4888-8888-888888888888");

    // Пустой зал бывает не только у нового клуба: так выглядит утро до открытия, и делить
    // на ноль мест сводка не имеет права.
    [Fact]
    public async Task Summary_ForBranchWithoutSeatsOrMoney_IsAllZerosAndDoesNotDivideByZero()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var summary = await service.GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(0, summary.Utilization.TotalSeats);
        Assert.Equal(0, summary.Utilization.UtilizationPercent);
        Assert.Equal(0, summary.Revenue.TotalRevenue.MinorUnits);
        Assert.Equal(0, summary.AlertPressure.TotalAlerts);
        Assert.Empty(summary.FocusQueue);
        Assert.Empty(summary.RecentPayments);
        Assert.Equal("none", summary.Shift.State);
        Assert.Equal(0, summary.Reservations.AvailableSlots);
    }

    [Fact]
    public async Task Summary_WhenEveryPcIsBusy_ShowsFullUtilizationWithoutOverflow()
    {
        await using var db = CreateDbContext();
        AddSeats(db, 3);
        // Сессий больше, чем мест: так бывает при переносе игрока между ПК, пока старая сессия
        // ещё завершается. Загрузка от этого не должна стать 133 %.
        AddSession(db, SessionStateNames.Active, DayStart.AddHours(9));
        AddSession(db, SessionStateNames.Active, DayStart.AddHours(10));
        AddSession(db, SessionStateNames.Active, DayStart.AddHours(11));
        AddSession(db, SessionStateNames.Active, DayStart.AddHours(12));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(100, summary.Utilization.UtilizationPercent);
        Assert.Equal(0, summary.Reservations.AvailableSlots);
    }

    [Fact]
    public async Task Summary_CountsFreeSeatsAgainstSessionsAndReservedSeats()
    {
        await using var db = CreateDbContext();
        var seats = AddSeats(db, 5);
        AddSession(db, SessionStateNames.Active, DayStart.AddHours(9), seats[0]);
        AddSession(db, SessionStateNames.Ending, DayStart.AddHours(10), seats[1]);
        AddReservation(db, seats[2], ReservationStateNames.Confirmed);
        AddReservation(db, seats[3], ReservationStateNames.Pending);
        // Отменённая бронь место не держит.
        AddReservation(db, seats[4], ReservationStateNames.Cancelled);
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(2, summary.Reservations.ActiveReservations);
        Assert.Equal(1, summary.Reservations.AvailableSlots);
    }

    /// <summary>
    /// Свободные места считаются в том же окне, что и деньги, — а окно по умолчанию это
    /// «сегодня до сейчас». Бронь на вечер в него не попадает, и в 18:00 сводка покажет место
    /// свободным, хотя в 19:00 туда придёт человек. Тест фиксирует поведение как есть: менять
    /// его — продуктовое решение (считать брони «вперёд», а не по окну отчёта), а не правка теста.
    /// </summary>
    [Fact]
    public async Task Summary_DoesNotCountTonightsReservationInTheDefaultRange()
    {
        await using var db = CreateDbContext();
        var seats = AddSeats(db, 2);
        AddReservation(db, seats[0], ReservationStateNames.Confirmed, DayStart.AddHours(21), DayStart.AddHours(23));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(0, summary.Reservations.ActiveReservations);
        Assert.Equal(2, summary.Reservations.AvailableSlots);

        // Спросили про весь день — бронь видна, и место под неё уже не считается свободным.
        var wholeDay = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new DashboardSummaryQuery(DayStart, DayStart.AddDays(1), null),
            CancellationToken.None);

        Assert.Equal(1, wholeDay.Reservations.ActiveReservations);
        Assert.Equal(1, wholeDay.Reservations.AvailableSlots);
    }

    [Fact]
    public async Task Summary_SubtractsRefundsFromBothPosAndGameplayRevenue()
    {
        await using var db = CreateDbContext();
        AddPayment(db, "payment", 5_000, DayStart.AddHours(11));
        AddPayment(db, "refund", -1_500, DayStart.AddHours(12));
        // Внесение денег в кассу не выручка: это движение по смене.
        AddPayment(db, "cash_in", 100_000, DayStart.AddHours(13));
        AddLedgerEntry(db, LedgerEntryTypeNames.GameplayCharge, -8_000, DayStart.AddHours(11));
        AddLedgerEntry(db, LedgerEntryTypeNames.Refund, 2_000, DayStart.AddHours(12));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(3_500, summary.Revenue.PosNetSales.MinorUnits);
        Assert.Equal(6_000, summary.Revenue.GameplayRevenue.MinorUnits);
        Assert.Equal(9_500, summary.Revenue.TotalRevenue.MinorUnits);
    }

    [Fact]
    public async Task Summary_IgnoresMoneyOutsideTheAskedRange()
    {
        await using var db = CreateDbContext();
        AddPayment(db, "payment", 5_000, DayStart.AddHours(11));
        AddPayment(db, "payment", 900_000, DayStart.AddDays(-3));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new DashboardSummaryQuery(DayStart, Now, null),
            CancellationToken.None);

        Assert.Equal(5_000, summary.Revenue.PosNetSales.MinorUnits);
        Assert.Single(summary.RecentPayments);
    }

    // Перепутанные местами границы — обычная опечатка в адресной строке, а не злой умысел:
    // сводка должна показать день, а не пустой экран.
    [Fact]
    public async Task Summary_WithSwappedRange_StillReturnsThatRange()
    {
        await using var db = CreateDbContext();
        AddPayment(db, "payment", 5_000, DayStart.AddHours(11));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new DashboardSummaryQuery(Now, DayStart, null),
            CancellationToken.None);

        Assert.Equal(DayStart, summary.FromUtc);
        Assert.Equal(Now, summary.ToUtc);
        Assert.Equal(5_000, summary.Revenue.PosNetSales.MinorUnits);
    }

    [Fact]
    public async Task Summary_ShowsNewestPaymentsFirst()
    {
        await using var db = CreateDbContext();
        AddPayment(db, "payment", 1_000, DayStart.AddHours(9));
        AddPayment(db, "payment", 2_000, DayStart.AddHours(14));
        AddPayment(db, "payment", 3_000, DayStart.AddHours(12));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal([2_000, 3_000, 1_000], summary.RecentPayments.Select(payment => payment.Amount.MinorUnits));
    }

    [Theory]
    // Ноль и отрицательное значение — это «не указано»: восемь строк по умолчанию.
    [InlineData(0, 8)]
    [InlineData(-5, 8)]
    [InlineData(3, 3)]
    // Потолок в двадцать строк: очередь внимания, в которую не помещается экран, никто не читает.
    [InlineData(999, 20)]
    public async Task Summary_KeepsTheQueueAndPaymentsWithinTheAskedLimit(int requestedLimit, int expectedCount)
    {
        await using var db = CreateDbContext();
        for (var index = 0; index < 25; index++)
        {
            AddPayment(db, "payment", 1_000 + index, DayStart.AddMinutes(index));
            AddOfflineDevice(db, $"PC-{index:00}");
        }
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new DashboardSummaryQuery(null, null, requestedLimit),
            CancellationToken.None);

        Assert.Equal(expectedCount, summary.RecentPayments.Count);
        Assert.Equal(expectedCount, summary.FocusQueue.Count);
    }

    [Fact]
    public async Task Summary_PutsFailedCommandsAheadOfMerelyOfflineDevices()
    {
        await using var db = CreateDbContext();
        var deviceId = AddOfflineDevice(db, "PC-01");
        AddCommand(db, deviceId, "lock", "Failed", "Экран не погас.", Now.AddMinutes(-5));
        AddOfflineDevice(db, "PC-02");
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal("device-command", summary.FocusQueue[0].SourceType);
        Assert.Equal("blocking", summary.FocusQueue[0].Tone);
        Assert.Equal("Экран не погас.", summary.FocusQueue[0].Detail);
        Assert.Contains(summary.FocusQueue, item => item.SourceType == "device" && item.Target == "PC-02");
    }

    [Fact]
    public async Task Summary_CountsOfflineDevicesAndStuckCommandsAsAlerts()
    {
        await using var db = CreateDbContext();
        var deviceId = AddOfflineDevice(db, "PC-01");
        AddCommand(db, deviceId, "lock", "Pending", string.Empty, Now.AddMinutes(-2));
        AddCommand(db, deviceId, "unlock", "Rejected", "Агент отказал.", Now.AddMinutes(-1));
        AddSession(db, SessionStateNames.Ending, DayStart.AddHours(10));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(1, summary.AlertPressure.PendingCommands);
        Assert.Equal(1, summary.AlertPressure.FailedCommands);
        Assert.Equal(1, summary.AlertPressure.OfflineDevices);
        Assert.Equal(1, summary.AlertPressure.EndingSessions);
        Assert.Equal(4, summary.AlertPressure.TotalAlerts);
    }

    // Соседний филиал той же сети не должен подмешиваться: у него своя касса и свои ПК.
    [Fact]
    public async Task Summary_LooksOnlyAtItsOwnBranch()
    {
        await using var db = CreateDbContext();
        AddSeats(db, 2);
        AddPayment(db, "payment", 5_000, DayStart.AddHours(11));
        db.Seats.Add(new SeatEntity
        {
            SeatId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = OtherBranchId,
            ZoneId = Guid.NewGuid(),
            Name = "Чужой PC",
            SortOrder = 1,
            CreatedAtUtc = DayStart
        });
        db.Payments.Add(NewPayment("payment", 777_000, DayStart.AddHours(12), OtherBranchId));
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(2, summary.Utilization.TotalSeats);
        Assert.Equal(5_000, summary.Revenue.PosNetSales.MinorUnits);
    }

    [Fact]
    public async Task Summary_ReportsOpenShiftAndItsExpectedCash()
    {
        await using var db = CreateDbContext();
        db.Shifts.AddRange(
            new ShiftEntity
            {
                ShiftId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                State = ShiftStateNames.Closed,
                OpenedByStaffUserId = StaffId,
                OpenedAtUtc = DayStart.AddHours(2),
                CurrencyCode = "TJS",
                ExpectedCashMinorUnits = 1_000
            },
            new ShiftEntity
            {
                ShiftId = OpenShiftId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                State = ShiftStateNames.Open,
                OpenedByStaffUserId = StaffId,
                OpenedAtUtc = DayStart.AddHours(8),
                CurrencyCode = "TJS",
                ExpectedCashMinorUnits = 42_500
            });
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync(
            TestIds.OrganizationId, TestIds.BranchId, new DashboardSummaryQuery(null, null, null), CancellationToken.None);

        Assert.Equal(OpenShiftId, summary.Shift.ShiftId);
        Assert.Equal(ShiftStateNames.Open, summary.Shift.State);
        Assert.Equal(42_500, summary.Shift.ExpectedCash.MinorUnits);
        Assert.Equal("TJS", summary.Shift.ExpectedCash.CurrencyCode);
    }

    // --- обвязка ---------------------------------------------------------------------------

    private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly Guid OpenShiftId = Guid.Parse("12121212-1212-4212-8212-121212121212");

    private static EfOperatorDashboardService CreateService(PlatformDbContext db) =>
        new(db, new FixedTimeProvider(Now));

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static List<Guid> AddSeats(PlatformDbContext db, int count)
    {
        var seatIds = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var seatId = Guid.NewGuid();
            seatIds.Add(seatId);
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = Guid.NewGuid(),
                Name = $"PC-{index + 1:00}",
                SortOrder = index,
                CreatedAtUtc = DayStart
            });
        }

        return seatIds;
    }

    private static void AddSession(PlatformDbContext db, string state, DateTimeOffset startedAtUtc, Guid? seatId = null) =>
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = seatId ?? Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = StaffId,
            State = state,
            BillingMode = "prepaid",
            Origin = "counter",
            RequestedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc
        });

    private static void AddReservation(
        PlatformDbContext db,
        Guid seatId,
        string state,
        DateTimeOffset? startsAtUtc = null,
        DateTimeOffset? endsAtUtc = null) =>
        db.Reservations.Add(new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = seatId,
            CustomerName = "Гость",
            StartsAtUtc = startsAtUtc ?? DayStart.AddHours(17),
            EndsAtUtc = endsAtUtc ?? DayStart.AddHours(20),
            State = state,
            Source = "app",
            CreatedByStaffUserId = StaffId,
            CreatedAtUtc = DayStart,
            UpdatedAtUtc = DayStart
        });

    private static void AddPayment(PlatformDbContext db, string kind, long minorUnits, DateTimeOffset createdAtUtc) =>
        db.Payments.Add(NewPayment(kind, minorUnits, createdAtUtc, TestIds.BranchId));

    private static PaymentEntity NewPayment(string kind, long minorUnits, DateTimeOffset createdAtUtc, Guid branchId) =>
        new()
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId,
            ShiftId = OpenShiftId,
            CreatedByStaffUserId = StaffId,
            PaymentKind = kind,
            Provider = "manual",
            PaymentMethod = "cash",
            CurrencyCode = "TJS",
            AmountMinorUnits = minorUnits,
            CreatedAtUtc = createdAtUtc
        };

    private static void AddLedgerEntry(PlatformDbContext db, string entryType, long minorUnits, DateTimeOffset createdAtUtc) =>
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerId,
            EntryType = entryType,
            AccountType = "wallet",
            AmountMinorUnits = minorUnits,
            CurrencyCode = "TJS",
            CreatedByStaffUserId = StaffId,
            CreatedAtUtc = createdAtUtc
        });

    private static Guid AddOfflineDevice(PlatformDbContext db, string machineName)
    {
        var deviceId = Guid.NewGuid();
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = deviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = machineName,
            AgentVersion = "0.1.29",
            ShellVersion = "0.1.29",
            EnrolledAtUtc = DayStart,
            LastHeartbeatAtUtc = DayStart.AddHours(1),
            IsOnline = false,
            IsLocked = true
        });

        return deviceId;
    }

    private static void AddCommand(
        PlatformDbContext db,
        Guid deviceId,
        string type,
        string status,
        string message,
        DateTimeOffset updatedAtUtc) =>
        db.DeviceCommands.Add(new DeviceCommandEntity
        {
            CommandId = Guid.NewGuid(),
            DeviceId = deviceId,
            Type = type,
            Status = status,
            Message = message,
            CreatedAtUtc = updatedAtUtc.AddMinutes(-1),
            UpdatedAtUtc = updatedAtUtc
        });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
