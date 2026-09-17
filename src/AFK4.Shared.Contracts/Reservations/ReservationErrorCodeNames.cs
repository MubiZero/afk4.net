namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Машинные имена отказов по броням. См. <see cref="Shifts.ShiftErrorCodeNames"/> — та же причина:
/// стойка работает на трёх языках, а английская фраза сервера в интерфейс попадать не должна.
///
/// Почти все эти отказы — про состояние брони, которое успело измениться: сосед подтвердил её
/// раньше, гость уже сидит, заявку уже отклонили. Отличать их друг от друга оператору нужно
/// именно потому, что дальше он делает разное.
/// </summary>
public static class ReservationErrorCodeNames
{
    /// <summary>Подтвердить можно только заявку, на которую клуб ещё не ответил.</summary>
    public const string NotPending = "reservation_not_pending";

    /// <summary>Менять можно бронь, которая ещё не отменена и не закрыта.</summary>
    public const string NotChangeable = "reservation_not_changeable";

    /// <summary>Посадить можно только заявку или подтверждённую бронь.</summary>
    public const string NotSeatable = "reservation_not_seatable";

    /// <summary>Бронь без места — сажать некуда.</summary>
    public const string SeatRequired = "reservation_seat_required";

    /// <summary>Отменить можно только заявку или подтверждённую бронь.</summary>
    public const string NotCancellable = "reservation_not_cancellable";

    /// <summary>Отмена без причины: её читает гость и она же уходит в журнал.</summary>
    public const string CancelReasonRequired = "reservation_cancel_reason_required";

    /// <summary>Отказать можно в заявке, на которую клуб ещё не ответил.</summary>
    public const string NotRejectable = "reservation_not_rejectable";

    /// <summary>Отказ «своими словами» без слов.</summary>
    public const string RefusalNoteRequired = "reservation_refusal_note_required";

    /// <summary>Неизвестная причина отказа.</summary>
    public const string RejectReasonUnsupported = "reservation_reject_reason_unsupported";

    /// <summary>Неявку отмечают у подтверждённой брони, время которой уже началось.</summary>
    public const string NoShowNotAllowed = "reservation_no_show_not_allowed";
}
