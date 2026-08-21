using System.Data.Common;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests.Reservations;

/// <summary>
/// Администратор подтверждает заявку ровно в ту секунду, когда фоновая задача снимает её по сроку.
///
/// Обе стороны читают одну и ту же строку в состоянии «ждёт ответа» и обе решают, что вправе её
/// изменить. Порознь это выглядит безобидно, а вместе даёт бронь, которая подтверждена и отменена
/// одновременно, — и деньги, которые вернулись игроку, оставшись занятыми под живую бронь.
///
/// Спор решает сама база: у брони есть версия-сторож, и вторая по времени запись просто не находит
/// строку в том виде, в каком читала. In-memory провайдер этого не покажет, поэтому проверка идёт
/// на настоящей PostgreSQL, а барьер ниже заставляет обе стороны прочитать заявку ДО того, как
/// любая из них запишет.
/// </summary>
public sealed class PostgresRequestExpiryConcurrencyTests
{
    private const long HoldAmount = 600;
    private const long TopUp = 5_000;

    [PostgresSessionFact]
    public async Task ConfirmingAtTheDeadline_EitherAnswersOrExpires_ButNeverBoth()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();

        var reservationId = Guid.Parse("6a5c1d40-1111-4111-8111-111111111111");
        await SeedExpiringRequestAsync(database, reservationId);

        using var barrier = new Barrier(2);
        await using var confirmDb = database.CreateDbContext(new ReservationReadBarrier(barrier));
        await using var expiryDb = database.CreateDbContext(new ReservationReadBarrier(barrier));

        var confirmTask = Task.Run(() => new EfReservationService(confirmDb, new FixedTimeProvider(database.Now))
            .ConfirmAsync(
                reservationId,
                database.StaffUserId,
                new ConfirmReservationRequest(database.OrganizationId, ExpectedVersion: 1),
                CancellationToken.None));
        var expiryTask = Task.Run(() => new ReservationRequestExpiryRunner(expiryDb, new FixedTimeProvider(database.Now))
            .RunOnceAsync(CancellationToken.None));

        var confirm = await confirmTask.WaitAsync(TimeSpan.FromSeconds(60));
        var expired = await expiryTask.WaitAsync(TimeSpan.FromSeconds(60));

        await using var verifyDb = database.CreateDbContext();
        var reservation = await verifyDb.Reservations.SingleAsync(row => row.ReservationId == reservationId);
        var balances = await LedgerBalanceProjector.GetClubBalancesAsync(
            verifyDb, database.PlayerAccountId, CancellationToken.None);
        var releases = await verifyDb.LedgerEntries
            .CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal);

        // Ровно один победитель: либо клуб успел ответить, либо срок вышел.
        Assert.True(confirm.Succeeded ^ expired == 1);

        if (confirm.Succeeded)
        {
            Assert.Equal(ReservationStateNames.Confirmed, reservation.State);
            Assert.Equal(database.Now, reservation.ConfirmedAtUtc);
            // Бронь жива — значит и деньги под неё остаются занятыми.
            Assert.Equal(0, releases);
            Assert.Equal(HoldAmount, balances.HeldMinorUnits);
            Assert.Equal(TopUp - HoldAmount, balances.WalletMinorUnits);
        }
        else
        {
            Assert.Equal(ReservationStateNames.Cancelled, reservation.State);
            Assert.Equal(ReservationRequestExpiryRunner.CancelReason, reservation.CancelReason);
            Assert.Null(reservation.ConfirmedAtUtc);
            // Заявка снята — деньги вернулись целиком и ровно один раз.
            Assert.Equal(1, releases);
            Assert.Equal(0, balances.HeldMinorUnits);
            Assert.Equal(TopUp, balances.WalletMinorUnits);
        }
    }

    private static async Task SeedExpiringRequestAsync(SessionStartPostgresFixture database, Guid reservationId)
    {
        await using var db = database.CreateDbContext();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            PlayerAccountId = database.PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = TopUp,
            CurrencyCode = "TJS",
            CreatedAtUtc = database.Now.AddDays(-1)
        });

        var reservation = new ReservationEntity
        {
            ReservationId = reservationId,
            OrganizationId = database.OrganizationId,
            BranchId = database.BranchId,
            PlayerAccountId = database.PlayerAccountId,
            SeatId = database.SeatId,
            CustomerName = "Игрок",
            StartsAtUtc = database.Now.AddHours(2),
            EndsAtUtc = database.Now.AddHours(3),
            State = ReservationStateNames.Pending,
            Source = ReservationSourceNames.Online,
            Note = string.Empty,
            CancelReason = string.Empty,
            CreatedByStaffUserId = Guid.Empty,
            UpdatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = database.Now.AddMinutes(-31),
            UpdatedAtUtc = database.Now.AddMinutes(-31),
            // Срок вышел минуту назад: фоновая задача берётся за заявку в ту же секунду, в которую
            // администратор жмёт «подтвердить».
            RespondByUtc = database.Now.AddMinutes(-1),
            EstimatedCostMinorUnits = HoldAmount,
            CurrencyCode = "TJS"
        };
        db.Reservations.Add(reservation);
        db.LedgerEntries.Add(ReservationHold.Create(reservation, HoldAmount, "TJS", database.Now.AddMinutes(-31)));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Отпускает обе стороны только после того, как каждая прочитала таблицу броней: до записи,
    /// но уже зная состояние заявки.
    /// </summary>
    private sealed class ReservationReadBarrier(Barrier barrier) : DbCommandInterceptor
    {
        private bool tripped;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!tripped && command.CommandText.Contains("reservations", StringComparison.OrdinalIgnoreCase))
            {
                tripped = true;
                // С таймаутом: если вторая сторона до чтения не дошла, тест обязан упасть, а не
                // подвесить весь прогон.
                barrier.SignalAndWait(TimeSpan.FromSeconds(30));
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
