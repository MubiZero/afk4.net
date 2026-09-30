using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Outbox;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Sessions;

/// <summary>
/// Решение владельца 30.09.2026: гость платит наличными у стойки. До него «Посадить гостя» в Панели
/// сажало бесплатно — ни тарифа, ни суммы, ни записи в учёте (приёмка 30.09.2026, P6).
///
/// Тесты идут на настоящем PostgreSQL и проходят весь путь, которым идут деньги: старт, продление,
/// расчёт, затем касса смены, Z-отчёт и «Сводка» — потому что именно их расхождение прячет
/// потерянные деньги. Тариф везде один: 50 дирамов в минуту, минимум 30 минут, шаг 15.
/// </summary>
public sealed class GuestPaysCashPostgresTests
{
    private const long StartingCash = 10_000;
    private const long HourPrice = 3_000;

    [PostgresSessionFact]
    public async Task FixedTimeGuest_PaysCashAtStart_AndTheMoneyReachesTheTillTheZReportAndTheSummaryAlike()
    {
        await using var world = await World.CreateAsync();

        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-start-1");

        Assert.True(start.Succeeded, start.Error);
        var session = await world.ReloadAsync(db => db.Sessions.SingleAsync());
        Assert.Equal(BillingModeNames.PrepaidCash, session.BillingMode);
        Assert.Null(session.PlayerAccountId);
        Assert.Equal(world.TariffVersionId.ToString("D"), session.TariffRuleVersionId);

        // Деньги гостя: одна запись выручки за игру без игрока и одна оплата наличными в ящик смены.
        var charge = await world.ReloadAsync(db => db.LedgerEntries.SingleAsync());
        Assert.Equal(LedgerEntryTypeNames.GameplayCharge, charge.EntryType);
        Assert.Null(charge.PlayerAccountId);
        Assert.Equal(-HourPrice, charge.AmountMinorUnits);
        Assert.Equal(world.ShiftId, charge.ShiftId);
        var payment = await world.ReloadAsync(db => db.Payments.SingleAsync());
        Assert.Equal(PaymentMethodNames.Cash, payment.PaymentMethod);
        Assert.Equal(HourPrice, payment.AmountMinorUnits);
        Assert.Equal(world.ShiftId, payment.ShiftId);
        Assert.Equal(session.SessionId, payment.SessionId);
        Assert.Null(payment.PosSaleId);

        await world.AssertBooksAgreeAsync(cashTaken: HourPrice, earnedForGame: HourPrice);
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_WithoutAnOpenShift_IsRefusedWithAReasonAndTakesNothing()
    {
        await using var world = await World.CreateAsync(openShift: false);

        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-no-shift");

        Assert.False(start.Succeeded);
        // Машинный код, а не голое «не принято»: по нему Панель говорит кассиру открыть смену.
        Assert.Equal("open_shift_required", start.Code);
        Assert.Empty(await world.ReloadAsync(db => db.Sessions.ToListAsync()));
        Assert.Empty(await world.ReloadAsync(db => db.LedgerEntries.ToListAsync()));
        Assert.Empty(await world.ReloadAsync(db => db.Payments.ToListAsync()));
        Assert.Empty(await world.ReloadAsync(db => db.DeviceCommands.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_RepeatedStartWithTheSameKey_TakesTheMoneyOnce()
    {
        await using var world = await World.CreateAsync();

        var first = await world.StartFixedGuestAsync(minutes: 60, key: "guest-retry");
        var second = await world.StartFixedGuestAsync(minutes: 60, key: "guest-retry");

        Assert.True(first.Succeeded, first.Error);
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(first.Response!.Session.SessionId, second.Response!.Session.SessionId);
        Assert.Single(await world.ReloadAsync(db => db.LedgerEntries.ToListAsync()));
        Assert.Single(await world.ReloadAsync(db => db.Payments.ToListAsync()));
        await world.AssertBooksAgreeAsync(cashTaken: HourPrice, earnedForGame: HourPrice);
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_WhenThePriceTheOperatorQuotedIsNoLongerTheTariffsPrice_IsRefused()
    {
        await using var world = await World.CreateAsync();

        // Оператор назвал гостю 25, а тариф к нажатию стоит 30: лучше отказ, чем другие деньги.
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-stale-price", expectedCharge: 2_500);

        Assert.False(start.Succeeded);
        Assert.Equal(SessionErrorCodeNames.PriceChanged, start.Code);
        Assert.Empty(await world.ReloadAsync(db => db.Sessions.ToListAsync()));
        Assert.Empty(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_TheQuotedPriceAppliesTheTariffMinimumAndRounding()
    {
        await using var world = await World.CreateAsync();

        // 10 минут на тарифе с минимумом 30 — это 30 минут: сумма считается по правилам тарифа, и
        // Панель называет гостю именно её.
        var start = await world.StartFixedGuestAsync(minutes: 10, key: "guest-minimum", expectedCharge: 1_500);

        Assert.True(start.Succeeded, start.Error);
        Assert.Equal(1_500, (await world.ReloadAsync(db => db.Payments.SingleAsync())).AmountMinorUnits);
    }

    [PostgresSessionFact]
    public async Task CashBilling_IsForAGuestOnly_AClubClientIsRefused()
    {
        await using var world = await World.CreateAsync();

        var start = await world.Commands.StartGuestSessionAsync(
            world.Fixture.BranchId,
            world.Fixture.StaffUserId,
            new StartGuestSessionRequest(
                world.Fixture.OrganizationId, world.Fixture.SeatId, world.TariffVersionId.ToString("D"), "cash-with-player",
                SessionDurationModes.Fixed, 60, world.Fixture.PlayerAccountId, BillingModeNames.PrepaidCash, world.TariffVersionId),
            SessionOriginNames.Operator,
            CancellationToken.None);

        Assert.False(start.Succeeded);
        Assert.Equal(SessionErrorCodeNames.CashBillingGuestOnly, start.Code);
        Assert.Empty(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_ExtensionIsPaidInCashByTheSessionsOwnTariff_EvenWhenTheButtonSendsNoTariff()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-ext-start");
        Assert.True(start.Succeeded, start.Error);
        var sessionId = start.Response!.Session.SessionId;

        // Быстрое «+15 мин» в карточке ПК шлёт пустой режим оплаты и тариф-заглушку — сервер берёт
        // способ оплаты и тариф из самой сессии.
        var quote = await world.Checkout.QuoteExtendAsync(sessionId, world.Fixture.OrganizationId, 15, CancellationToken.None);
        Assert.True(quote.Succeeded, quote.Error);
        Assert.Equal(750, quote.Response!.Charge.MinorUnits);

        var extend = await world.ExtendAsync(sessionId, minutes: 15, key: "guest-ext-1", expectedCharge: 750);

        Assert.True(extend.Succeeded, extend.Error);
        Assert.Equal(start.Response.Session.EndsAtUtc!.Value.AddMinutes(15), extend.Response!.Session.EndsAtUtc);
        var payments = await world.ReloadAsync(db => db.Payments.OrderBy(payment => payment.CreatedAtUtc).ToListAsync());
        Assert.Equal([HourPrice, 750L], payments.Select(payment => payment.AmountMinorUnits).ToArray());
        Assert.All(payments, payment => Assert.Equal(PaymentMethodNames.Cash, payment.PaymentMethod));
        var charges = await world.ReloadAsync(db => db.LedgerEntries.ToListAsync());
        Assert.Equal(-(HourPrice + 750), charges.Sum(entry => entry.AmountMinorUnits));
        Assert.All(charges, entry => Assert.Null(entry.PlayerAccountId));

        // Повтор того же нажатия после обрыва связи деньги второй раз не берёт.
        var replay = await world.ExtendAsync(sessionId, minutes: 15, key: "guest-ext-1", expectedCharge: 750);
        Assert.True(replay.Succeeded, replay.Error);
        Assert.Equal(2, (await world.ReloadAsync(db => db.Payments.ToListAsync())).Count);

        await world.AssertBooksAgreeAsync(cashTaken: HourPrice + 750, earnedForGame: HourPrice + 750);
    }

    // Продление не меняет, чья сессия: игрок в запросе не привязывается к гостевой сессии.
    [PostgresSessionFact]
    public async Task Extension_NeverAttachesAPlayerToAGuestSession()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-owner-start");
        Assert.True(start.Succeeded, start.Error);

        var extend = await world.Commands.ExtendSessionAsync(
            start.Response!.Session.SessionId,
            world.Fixture.StaffUserId,
            new ExtendSessionRequest(
                15, "manual-v1", "guest-owner-extend", PlayerAccountId: world.Fixture.PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet, TariffVersionId: world.TariffVersionId),
            CancellationToken.None);

        Assert.False(extend.Succeeded);
        Assert.Null((await world.ReloadAsync(db => db.Sessions.SingleAsync())).PlayerAccountId);
        Assert.Single(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_ExtensionWithoutAnOpenShift_IsRefusedAndQuotesTheReason()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-ext-noshift-start");
        Assert.True(start.Succeeded, start.Error);
        await world.CloseShiftAsync();

        var quote = await world.Checkout.QuoteExtendAsync(
            start.Response!.Session.SessionId, world.Fixture.OrganizationId, 15, CancellationToken.None);
        var extend = await world.ExtendAsync(start.Response.Session.SessionId, minutes: 15, key: "guest-ext-noshift");

        Assert.False(quote.Succeeded);
        Assert.Equal("open_shift_required", quote.Code);
        Assert.False(extend.Succeeded);
        Assert.Equal("open_shift_required", extend.Code);
        Assert.Single(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_ExtensionAtAPriceThatChanged_IsRefused()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-ext-price-start");
        Assert.True(start.Succeeded, start.Error);

        var extend = await world.ExtendAsync(start.Response!.Session.SessionId, minutes: 15, key: "guest-ext-price", expectedCharge: 500);

        Assert.False(extend.Succeeded);
        Assert.Equal(SessionErrorCodeNames.PriceChanged, extend.Code);
        Assert.Single(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FixedTimeGuest_CheckoutChargesNothingMore_AndTheQuoteSaysWhatWasPaidUpFront()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-fixed-checkout");
        Assert.True(start.Succeeded, start.Error);
        world.Clock.Now += TimeSpan.FromMinutes(20);

        var quote = await world.Checkout.QuoteAsync(start.Response!.Session.SessionId, world.Fixture.OrganizationId, CancellationToken.None);

        Assert.True(quote.Succeeded, quote.Error);
        // Время уже оплачено: к оплате ноль, но окно расчёта не молчит — сыграно 20 минут, вперёд
        // уплачено 30, автоматического возврата гостю нет.
        Assert.Equal(0, quote.Response!.GrandTotal.MinorUnits);
        Assert.Equal(20 * 60, quote.Response.PlayedSeconds);
        Assert.Equal(HourPrice, quote.Response.PrepaidCharged!.MinorUnits);
        Assert.Null(quote.Response.PrepaidRefund);

        var checkout = await world.Checkout.CheckoutAsync(
            start.Response.Session.SessionId,
            world.Fixture.StaffUserId,
            new SessionCheckoutRequest(world.Fixture.OrganizationId, [], "guest-fixed-checkout-1"),
            CancellationToken.None);

        Assert.True(checkout.Succeeded, checkout.Error);
        Assert.Equal(SessionStateNames.Ending, (await world.ReloadAsync(db => db.Sessions.SingleAsync())).State);
        await world.AssertBooksAgreeAsync(cashTaken: HourPrice, earnedForGame: HourPrice);
    }

    [PostgresSessionFact]
    public async Task OpenTabGuest_PaysByTheTariffAtCheckout_AndTheGameShowsUpInTheBooks()
    {
        await using var world = await World.CreateAsync();
        var start = await world.Commands.StartGuestSessionAsync(
            world.Fixture.BranchId,
            world.Fixture.StaffUserId,
            new StartGuestSessionRequest(
                world.Fixture.OrganizationId, world.Fixture.SeatId, world.TariffVersionId.ToString("D"), "guest-open-start",
                SessionDurationModes.Open, null, PlayerAccountId: null, BillingMode: "", TariffVersionId: world.TariffVersionId),
            SessionOriginNames.Operator,
            CancellationToken.None);
        Assert.True(start.Succeeded, start.Error);
        // На старте денег нет: счёт открыт, расчёт по факту.
        Assert.Empty(await world.ReloadAsync(db => db.Payments.ToListAsync()));
        Assert.Empty(await world.ReloadAsync(db => db.LedgerEntries.ToListAsync()));
        var sessionId = start.Response!.Session.SessionId;
        world.Clock.Now += TimeSpan.FromMinutes(25);

        var quote = await world.Checkout.QuoteAsync(sessionId, world.Fixture.OrganizationId, CancellationToken.None);

        // 25 минут на тарифе с минимумом 30: считается 30 — 1500.
        Assert.True(quote.Succeeded, quote.Error);
        Assert.Equal(1_500, quote.Response!.GrandTotal.MinorUnits);
        Assert.Equal(25 * 60, quote.Response.PlayedSeconds);
        Assert.Null(quote.Response.PrepaidCharged);

        var checkout = await world.CheckoutCashAsync(sessionId, 1_500, "guest-open-checkout");

        Assert.True(checkout.Succeeded, checkout.Error);
        var charge = await world.ReloadAsync(db => db.LedgerEntries.SingleAsync());
        Assert.Null(charge.PlayerAccountId);
        Assert.Equal(LedgerEntryTypeNames.GameplayCharge, charge.EntryType);
        Assert.Equal(-1_500, charge.AmountMinorUnits);
        Assert.Equal(1_500, (await world.ReloadAsync(db => db.Payments.SingleAsync())).AmountMinorUnits);
        await world.AssertBooksAgreeAsync(cashTaken: 1_500, earnedForGame: 1_500);

        // Повтор расчёта с тем же ключом ничего не удваивает.
        var replay = await world.CheckoutCashAsync(sessionId, 1_500, "guest-open-checkout");
        Assert.True(replay.Succeeded, replay.Error);
        Assert.Single(await world.ReloadAsync(db => db.LedgerEntries.ToListAsync()));
        Assert.Single(await world.ReloadAsync(db => db.Payments.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task OpenTabGuest_StartRefusesATariffThatDoesNotExist_SoCheckoutCanNeverBeStuck()
    {
        await using var world = await World.CreateAsync();

        var start = await world.Commands.StartGuestSessionAsync(
            world.Fixture.BranchId,
            world.Fixture.StaffUserId,
            new StartGuestSessionRequest(
                world.Fixture.OrganizationId, world.Fixture.SeatId, Guid.NewGuid().ToString("D"), "guest-open-ghost-tariff",
                SessionDurationModes.Open, null, PlayerAccountId: null, BillingMode: "", TariffVersionId: Guid.NewGuid()),
            SessionOriginNames.Operator,
            CancellationToken.None);

        Assert.False(start.Succeeded);
        Assert.Empty(await world.ReloadAsync(db => db.Sessions.ToListAsync()));
    }

    [PostgresSessionFact]
    public async Task FloorMap_TellsThePanelHowTheSeatsSessionWasPaid()
    {
        await using var world = await World.CreateAsync();
        var start = await world.StartFixedGuestAsync(minutes: 60, key: "guest-floor-map");
        Assert.True(start.Succeeded, start.Error);

        await using var db = world.Fixture.CreateDbContext();
        var floor = await new FloorMap.EfFloorMapReadService(db, world.Clock).GetFloorMapAsync(
            world.Fixture.BranchId, CancellationToken.None);

        var seat = Assert.Single(floor!.FloorMap.Seats);
        Assert.Equal(BillingModeNames.PrepaidCash, seat.SessionBillingMode);
        Assert.Null(seat.PlayerDisplayName);
    }

    /// <summary>Часы, которые стоят, пока тест их не переведёт: расчёт открытого счёта смотрит на них.</summary>
    internal sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class World : IAsyncDisposable
    {
        private World(SessionStartPostgresFixture fixture, Clock clock)
        {
            Fixture = fixture;
            Clock = clock;
        }

        public SessionStartPostgresFixture Fixture { get; }

        public Clock Clock { get; }

        public Guid TariffVersionId { get; } = Guid.Parse("78888888-8888-4888-8888-888888888888");

        public Guid ShiftId { get; } = Guid.Parse("79999999-9999-4999-8999-999999999999");

        public EfSessionCommandService Commands { get; private set; } = null!;

        public EfSessionCheckoutService Checkout { get; private set; } = null!;

        private PlatformDbContext ServiceDb { get; set; } = null!;

        public static async Task<World> CreateAsync(bool openShift = true)
        {
            var fixture = await SessionStartPostgresFixture.CreateAsync(
                Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
            var world = new World(fixture, new Clock(fixture.Now));
            try
            {
                await fixture.SeedAsync();
                await world.SeedMoneyAsync(openShift);
                world.ServiceDb = fixture.CreateDbContext();
                var db = world.ServiceDb;
                var shifts = new EfShiftService(db, world.Clock);
                var loyalty = new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance);
                var billing = new SessionBillingService(db, new EfTariffService(db, world.Clock), shifts, loyalty, world.Clock);
                var dispatcher = new SavingCommandDispatchService(db);
                var lifecycle = new RecordingLifecycleNotifier();
                var workflow = new EfSessionStartWorkflow(
                    db, dispatcher, new FakeSessionLeaseSigner(), world.Clock, billing, lifecycle, new EfPlanLimitGuard(db));
                world.Commands = new EfSessionCommandService(
                    db, dispatcher, new FakeSessionLeaseSigner(), world.Clock, billing, lifecycle, workflow);
                world.Checkout = new EfSessionCheckoutService(
                    db, billing, loyalty, new ReceiptNumberGenerator(db), dispatcher, shifts, new EfBillingOutbox(db),
                    lifecycle, world.Clock);
                return world;
            }
            catch
            {
                await world.DisposeAsync();
                throw;
            }
        }

        private async Task SeedMoneyAsync(bool openShift)
        {
            await using var db = Fixture.CreateDbContext();
            var tariffId = Guid.NewGuid();
            db.Tariffs.Add(new TariffEntity
            {
                TariffId = tariffId,
                OrganizationId = Fixture.OrganizationId,
                BranchId = Fixture.BranchId,
                Name = "Standard",
                IsActive = true,
                CreatedAtUtc = Fixture.Now
            });
            db.TariffVersions.Add(new TariffVersionEntity
            {
                TariffVersionId = TariffVersionId,
                TariffId = tariffId,
                OrganizationId = Fixture.OrganizationId,
                BranchId = Fixture.BranchId,
                VersionNumber = 1,
                CurrencyCode = "TJS",
                PricePerMinuteMinorUnits = 50,
                MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15,
                EffectiveFromUtc = Fixture.Now.AddDays(-1),
                CreatedAtUtc = Fixture.Now
            });
            if (openShift)
            {
                db.Shifts.Add(NewShift());
            }

            await db.SaveChangesAsync();
        }

        private ShiftEntity NewShift() => new()
        {
            ShiftId = ShiftId,
            OrganizationId = Fixture.OrganizationId,
            BranchId = Fixture.BranchId,
            OpenedByStaffUserId = Fixture.StaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = StartingCash,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Fixture.Now
        };

        public Task<SessionCommandServiceResult> StartFixedGuestAsync(int minutes, string key, long? expectedCharge = null) =>
            Commands.StartGuestSessionAsync(
                Fixture.BranchId,
                Fixture.StaffUserId,
                new StartGuestSessionRequest(
                    Fixture.OrganizationId, Fixture.SeatId, TariffVersionId.ToString("D"), key,
                    SessionDurationModes.Fixed, minutes, PlayerAccountId: null, BillingMode: BillingModeNames.PrepaidCash,
                    TariffVersionId: TariffVersionId, ExpectedChargeMinorUnits: expectedCharge),
                SessionOriginNames.Operator,
                CancellationToken.None);

        // Ровно то, что шлёт быстрая кнопка «+15 мин»: режим и тариф пусты, заглушка вместо версии.
        public async Task<SessionCommandServiceResult> ExtendAsync(Guid sessionId, int minutes, string key, long? expectedCharge = null)
        {
            var session = await ReloadAsync(db => db.Sessions.SingleAsync(candidate => candidate.SessionId == sessionId));
            return await Commands.ExtendSessionAsync(
                sessionId,
                Fixture.StaffUserId,
                new ExtendSessionRequest(
                    minutes, "manual-v1", key, PlayerAccountId: null, BillingMode: "", TariffVersionId: null,
                    ExpectedVersion: null, ExpectedChargeMinorUnits: expectedCharge),
                CancellationToken.None);
        }

        public Task<SessionCheckoutResult> CheckoutCashAsync(Guid sessionId, long amount, string key) =>
            Checkout.CheckoutAsync(
                sessionId,
                Fixture.StaffUserId,
                new SessionCheckoutRequest(
                    Fixture.OrganizationId, [new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", amount))], key),
                CancellationToken.None);

        public async Task CloseShiftAsync()
        {
            await using var db = Fixture.CreateDbContext();
            var shift = await db.Shifts.SingleAsync();
            shift.State = ShiftStateNames.Closed;
            shift.ClosedAtUtc = Clock.Now;
            await db.SaveChangesAsync();
        }

        public async Task<T> ReloadAsync<T>(Func<PlatformDbContext, Task<T>> query)
        {
            await using var db = Fixture.CreateDbContext();
            return await query(db);
        }

        /// <summary>
        /// Ящик, Z-отчёт и «Сводка» говорят об одних и тех же деньгах: сколько наличных пришло в
        /// кассу и сколько заработала игра. Расхождение между ними — и есть потерянная выручка.
        /// </summary>
        public async Task AssertBooksAgreeAsync(long cashTaken, long earnedForGame)
        {
            await using var db = Fixture.CreateDbContext();
            var reports = new EfReportService(db);
            var z = await reports.GetCurrentShiftRevenueAsync(Fixture.OrganizationId, Fixture.BranchId, CancellationToken.None);
            Assert.NotNull(z);
            Assert.Equal(earnedForGame, z.Earned.Time.MinorUnits);
            Assert.Equal(earnedForGame, z.Earned.Total.MinorUnits);
            Assert.Equal(cashTaken, z.Inflow.Cash.MinorUnits);
            Assert.Equal(StartingCash + cashTaken, z.Cash.Expected.MinorUnits);

            var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                Clock.Now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dushanbe")).DateTime);
            var summary = await new OrganizationAdminReportService(db, reports).GetRevenueAsync(
                Fixture.OrganizationId, Fixture.BranchId, local, local, CancellationToken.None);
            Assert.Equal(earnedForGame, summary.GameplayRevenue.MinorUnits);
            Assert.Equal(earnedForGame, summary.NetRevenue.MinorUnits);
            // Сводка и Z-отчёт сходятся на одной цифре — её и видит владелец.
            Assert.Equal(z.Earned.Time.MinorUnits, summary.GameplayRevenue.MinorUnits);
        }

        public async ValueTask DisposeAsync()
        {
            if (ServiceDb is not null)
            {
                await ServiceDb.DisposeAsync();
            }

            await Fixture.DisposeAsync();
        }
    }
}
