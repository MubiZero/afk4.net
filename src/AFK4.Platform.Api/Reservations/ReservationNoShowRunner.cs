using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>Настройки разбора неявок.</summary>
public sealed class ReservationNoShowOptions
{
    /// <summary>Как часто проверяются брони, чьё время началось.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Сколько ждать игрока после начала брони. Решение владельца — 15 минут: опоздать на
    /// четверть часа нормально, держать место и деньги дольше — уже нет.
    /// </summary>
    public TimeSpan Grace { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Возвращает деньги и освобождает место, когда игрок не пришёл.
///
/// Без этого забытая бронь держала бы замороженную сумму до конца времён: игрок не пришёл, отменить
/// забыл, а деньги на кошельке выглядят как списанные.
///
/// Неявка оформляется отменой с причиной <c>no-show</c>, а не отдельным состоянием: слот должен
/// освободиться так же, как при отмене, а причина отличает автоматику от человека и в журнале, и на
/// экране оператора.
/// </summary>
public sealed class ReservationNoShowRunner(
    PlatformDbContext dbContext,
    ReservationNoShowOptions options,
    TimeProvider timeProvider)
{
    public const string CancelReason = "no-show";

    /// <summary>Один проход. Возвращает число разобранных броней.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var deadline = now - options.Grace;

        // Разбираются только брони, за которые заморожены деньги: у брони без холда неявка
        // ничего не стоит игроку, и трогать её автоматикой не за чем — с ней разберётся оператор.
        var overdue = await dbContext.Reservations
            .Where(reservation =>
                reservation.StartsAtUtc <= deadline &&
                reservation.EstimatedCostMinorUnits != null &&
                reservation.SeatedAtUtc == null &&
                (reservation.State == ReservationStateNames.Confirmed ||
                 reservation.State == ReservationStateNames.Pending))
            .ToListAsync(cancellationToken);
        if (overdue.Count == 0)
        {
            return 0;
        }

        var handled = 0;
        foreach (var reservation in overdue)
        {
            var released = await ReservationHold.ReleaseAsync(
                dbContext, reservation.ReservationId, ReservationHoldCauses.NoShow, now, cancellationToken);

            reservation.State = ReservationStateNames.Cancelled;
            reservation.CancelReason = CancelReason;
            reservation.CancelledAtUtc = now;
            // Guid.Empty — сделала система, а не сотрудник: в журнале это должно быть видно.
            reservation.UpdatedByStaffUserId = Guid.Empty;
            reservation.UpdatedAtUtc = now;
            reservation.Version++;
            handled++;

            // Холд мог быть уже снят вручную — бронь всё равно закрывается: место занимать нечем.
            _ = released;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }
}
