using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>Настройки разбора просроченных заявок.</summary>
public sealed class ReservationRequestExpiryOptions
{
    /// <summary>Как часто проверяются заявки, которым клуб обещал ответить.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Снимает заявки, на которые клуб не ответил в обещанный срок.
///
/// Деньги при этом возвращаются <b>целиком и всегда</b>. Это не неявка: при неявке человек не
/// приехал, и филиал вправе оставить предоплату себе (<c>KeepPrepaymentOnNoShow</c>). Здесь
/// молчала стойка, и удерживать за это нечего — какой бы ни была настройка неявки.
///
/// Просрочка оформляется отменой с причиной <c>request-expired</c>, а не отдельным состоянием:
/// слот должен освободиться так же, как при отмене, а причина отличает молчание клуба и от
/// передумавшего игрока, и от неявки — в журнале, на экране оператора и в будущей репутации.
///
/// Спор «администратор подтверждает / срок вышел» решает сама база: у брони есть версия-сторож,
/// и запись, пришедшая второй, не находит строку в том виде, в каком её читала. Поэтому каждая
/// заявка сохраняется отдельно — проигранный спор снимает с игры одну заявку, а не весь проход.
/// </summary>
public sealed class ReservationRequestExpiryRunner(
    PlatformDbContext dbContext,
    TimeProvider timeProvider)
{
    public const string CancelReason = "request-expired";

    /// <summary>Один проход. Возвращает число снятых заявок.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var expiredIds = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.State == ReservationStateNames.Pending &&
                reservation.RespondByUtc != null &&
                reservation.RespondByUtc <= now)
            .Select(reservation => reservation.ReservationId)
            .ToListAsync(cancellationToken);
        if (expiredIds.Count == 0)
        {
            return 0;
        }

        var handled = 0;
        foreach (var reservationId in expiredIds)
        {
            if (await ExpireAsync(reservationId, now, cancellationToken))
            {
                handled++;
            }
        }

        return handled;
    }

    private async Task<bool> ExpireAsync(
        Guid reservationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations
            .FirstOrDefaultAsync(candidate => candidate.ReservationId == reservationId, cancellationToken);

        // Клуб мог ответить за секунду до этого — тогда заявки в состоянии «ждёт» уже нет, и
        // трогать её нечем: подтверждённую бронь по таймеру никто не снимает.
        if (reservation is null ||
            reservation.State != ReservationStateNames.Pending ||
            reservation.RespondByUtc is not { } respondByUtc ||
            respondByUtc > now)
        {
            return false;
        }

        await ReservationHold.ReleaseAsync(
            dbContext, reservationId, ReservationHoldCauses.RequestExpired, now, cancellationToken);

        reservation.State = ReservationStateNames.Cancelled;
        reservation.CancelReason = CancelReason;
        reservation.CancelledAtUtc = now;
        // Guid.Empty — сняла система, а не сотрудник: в журнале это должно быть видно.
        reservation.UpdatedByStaffUserId = Guid.Empty;
        reservation.UpdatedAtUtc = now;
        reservation.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Стойка успела ответить между чтением и записью. Возврат денег уезжает вместе с
            // отменой — обе записи идут одним сохранением, поэтому деньги под живой бронью
            // остаются занятыми, а не возвращаются игроку наполовину.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
