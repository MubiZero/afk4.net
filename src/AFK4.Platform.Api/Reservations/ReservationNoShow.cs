using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Оформление неявки — в одном месте на весь проект.
///
/// Отметить неявку могут двое: таймер, дождавшийся положенных филиалу минут, и администратор,
/// который видит пустое место раньше любого таймера. Оформлять её они обязаны одинаково — иначе в
/// системе заведутся два представления о том, чем неявка отличается от отмены, и разойдутся они
/// на первом же исправлении. Ровно об этом предупреждал план волны 1: пункты, переписывающие одну
/// машину состояний, идут одним потоком и через один код.
///
/// Порядок действий важен: заморозка снимается всегда, а удержание выписывается только поверх
/// снятой заморозки. «Деньги просто не вернулись» — это состояние, которое нельзя объяснить и
/// нельзя посчитать в кассе; в журнале должны читаться оба шага.
/// </summary>
internal static class ReservationNoShow
{
    /// <summary>
    /// Отмечает бронь неявкой и возвращает удержанную сумму — или <c>null</c>, если удерживать
    /// было нечего: филиал так решил либо заморозка уже снята вручную или посадкой.
    /// </summary>
    /// <param name="shiftId">
    /// Смена, при которой это случилось, или <c>null</c>, если клуб в этот момент был закрыт: ни
    /// одна смена за такое удержание не отвечала.
    /// </param>
    public static async Task<long?> MarkAsync(
        PlatformDbContext dbContext,
        ReservationEntity reservation,
        BranchBookingSettingsDto settings,
        Guid? shiftId,
        Guid actorStaffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var released = await ReservationHold.ReleaseAsync(
            dbContext, reservation.ReservationId, ReservationHoldCauses.NoShow, now, cancellationToken);

        long? retained = null;
        if (released is not null && settings.KeepPrepaymentOnNoShow)
        {
            dbContext.LedgerEntries.Add(ReservationHold.CreateNoShowFee(
                released, reservation.ReservationId, shiftId, now));
            // Снятие заморозки вернуло сумму в остаток положительной записью — ровно её клуб и
            // оставляет себе. Человеку показывают «удержано 1500», а не «удержано минус 1500».
            retained = released.AmountMinorUnits;
        }

        reservation.State = ReservationStateNames.NoShow;
        reservation.NoShowAtUtc = now;
        reservation.RetainedAmountMinorUnits = retained;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = now;
        reservation.Version++;

        return retained;
    }

    /// <summary>
    /// Можно ли объявить эту бронь неявкой, или <c>null</c>, если можно.
    ///
    /// Отказы здесь не про права доступа, а про правду: заявка, на которую клуб не ответил, —
    /// это молчание клуба, а не чужая неявка, и превращать одно в другое одним кликом нельзя.
    /// </summary>
    public static string? WhyNot(ReservationEntity reservation, DateTimeOffset now) => reservation.State switch
    {
        ReservationStateNames.NoShow => null,
        ReservationStateNames.Confirmed when now < reservation.StartsAtUtc =>
            "The booking has not started yet.",
        ReservationStateNames.Confirmed => null,
        ReservationStateNames.Pending =>
            "A request the club has not answered cannot become the player's no-show.",
        ReservationStateNames.Seated => "The player is already seated.",
        _ => "Only a confirmed booking can be marked as a no-show."
    };
}
