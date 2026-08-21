using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Data;

/// <summary>
/// Форма схемы волны 2: у брони появляется правда о том, чем всё кончилось, а у сессии — правда
/// о том, с чего она началась.
///
/// Сегодня «не приехал» и «клуб отказал» изображаются одной и той же отменой с пометкой в
/// свободном тексте <c>CancelReason</c>. Отличить их можно только сравнением строк, а посчитать
/// честно — никак: игрок, которого клуб не принял, выглядит в точности как игрок, который не
/// приехал. Волна 2 разводит эти три исхода по своим состояниям и своим колонкам.
/// </summary>
public sealed class BookingTruthfulnessSchemaTests
{
    [Fact]
    public void Reservation_CarriesTheNoShowOutcome_WithoutForcingItOnAnyone()
    {
        var reservation = Model().FindEntityType(typeof(ReservationEntity))!;

        // Обе колонки необязательны: у подавляющего большинства броней неявки не случилось.
        Assert.True(reservation.FindProperty(nameof(ReservationEntity.NoShowAtUtc))!.IsNullable);
        Assert.True(reservation.FindProperty(nameof(ReservationEntity.RetainedAmountMinorUnits))!.IsNullable);
    }

    [Fact]
    public void Reservation_CarriesTheClubsRefusal_AsACodeAndWordsBesideIt()
    {
        var reservation = Model().FindEntityType(typeof(ReservationEntity))!;

        Assert.True(reservation.FindProperty(nameof(ReservationEntity.RejectedAtUtc))!.IsNullable);

        // Код — для машины: по нему считается статистика и подбирается текст на языке игрока.
        var code = reservation.FindProperty(nameof(ReservationEntity.RejectReasonCode))!;
        Assert.True(code.IsNullable);
        Assert.Equal(32, code.GetMaxLength());

        // Слова — для человека: администратор поясняет своими, когда кода не хватает.
        var note = reservation.FindProperty(nameof(ReservationEntity.RejectReasonNote))!;
        Assert.True(note.IsNullable);
        Assert.Equal(512, note.GetMaxLength());
    }

    /// <summary>
    /// Происхождение сессии обязательно и по умолчанию пустое: «неизвестно» и «посадил оператор» —
    /// разные ответы, и подставлять второй вместо первого нельзя. Пустая строка честно говорит
    /// «эта строка заведена до того, как вопрос начали задавать».
    /// </summary>
    [Fact]
    public void Session_SaysWhereItCameFrom()
    {
        var origin = Model().FindEntityType(typeof(SessionEntity))!
            .FindProperty(nameof(SessionEntity.Origin))!;

        Assert.False(origin.IsNullable);
        Assert.Equal(32, origin.GetMaxLength());
        Assert.Equal(string.Empty, origin.GetDefaultValue());
    }

    [Fact]
    public void Outcomes_AreDistinctStates_NotShadesOfCancelled()
    {
        string[] states =
        [
            ReservationStateNames.Pending,
            ReservationStateNames.Confirmed,
            ReservationStateNames.Seated,
            ReservationStateNames.Cancelled,
            ReservationStateNames.NoShow,
            ReservationStateNames.Rejected
        ];

        Assert.Equal(states.Length, states.Distinct(StringComparer.Ordinal).Count());
        Assert.All(states, state => Assert.Equal(state, state.ToLowerInvariant()));
    }

    [Fact]
    public void SessionOrigins_CoverEveryWayAPersonEndsUpAtAPc()
    {
        string[] origins =
        [
            SessionOriginNames.Operator,
            SessionOriginNames.PlayerPin,
            SessionOriginNames.Reservation
        ];

        Assert.Equal(origins.Length, origins.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(string.Empty, origins);
    }

    [PostgresSessionFact]
    public async Task Reservation_RoundTripsItsOutcome()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);

        var now = DateTimeOffset.Parse("2026-08-22T19:00:00Z");
        var reservationId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.Reservations.Add(new ReservationEntity
            {
                ReservationId = reservationId,
                OrganizationId = Guid.NewGuid(),
                BranchId = Guid.NewGuid(),
                CustomerName = "Фаррух",
                StartsAtUtc = now,
                EndsAtUtc = now.AddHours(1),
                State = ReservationStateNames.NoShow,
                Source = ReservationSourceNames.Online,
                NoShowAtUtc = now.AddMinutes(20),
                RetainedAmountMinorUnits = 1_500,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            var stored = await db.Reservations.AsNoTracking()
                .SingleAsync(row => row.ReservationId == reservationId);
            Assert.Equal(ReservationStateNames.NoShow, stored.State);
            Assert.Equal(now.AddMinutes(20), stored.NoShowAtUtc);
            Assert.Equal(1_500, stored.RetainedAmountMinorUnits);
            // Неявка — это не отмена: колонки отмены остаются пустыми.
            Assert.Null(stored.CancelledAtUtc);
            Assert.Equal(string.Empty, stored.CancelReason);
        }
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IModel Model()
    {
        using var db = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        return db.Model;
    }
}
