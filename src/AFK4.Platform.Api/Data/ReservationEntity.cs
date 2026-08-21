namespace AFK4.Platform.Api.Data;

public sealed class ReservationEntity
{
    public Guid ReservationId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public Guid? SeatId { get; set; }

    // Groups several seats booked together as one logical reservation (drag across rows). Null for a
    // single-seat booking. Members share this id so the group can be shown/managed as a unit.
    public Guid? ReservationGroupId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }

    public DateTimeOffset EndsAtUtc { get; set; }

    public string State { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public Guid CreatedByStaffUserId { get; set; }

    public Guid? UpdatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string CancelReason { get; set; } = string.Empty;

    public DateTimeOffset? SeatedAtUtc { get; set; }

    // Докуда клуб обещал ответить на заявку и когда ответил. Срок ставится при создании заявки,
    // ждущей решения администратора, и дальше ничего не значит: подтверждённую бронь никто не
    // снимает по таймеру. Обе даты уезжают и оператору, и игроку — иначе у двух администраторов
    // на разных машинах будут разные цифры, а у игрока третья.
    public DateTimeOffset? RespondByUtc { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    // Чем всё кончилось, когда кончилось не по-хорошему. Обе даты и сумма необязательны: у
    // подавляющего большинства броней ни неявки, ни отказа не случилось.
    //
    // Удержанная сумма дублирует запись журнала `reservation_no_show_fee` намеренно: карточка
    // заявки и полоса на экране оператора показывают её десятками за раз, и ходить за каждой в
    // журнал значит платить запросом за строку списка.
    public DateTimeOffset? NoShowAtUtc { get; set; }

    public long? RetainedAmountMinorUnits { get; set; }

    // Отказ клуба: когда, по какой причине из справочника и что администратор добавил словами.
    // Код нужен, чтобы причину можно было посчитать и перевести на язык игрока; текст — чтобы
    // человеку было что прочитать, когда кода не хватает.
    public DateTimeOffset? RejectedAtUtc { get; set; }

    public string? RejectReasonCode { get; set; }

    public string? RejectReasonNote { get; set; }

    public int Version { get; set; } = 1;

    public Guid? StartedSessionId { get; set; }

    // Billing choice the player made when booking from the app, and the price the server computed
    // for it. Null on operator-created bookings and on anything created before the app started
    // asking — those are still priced at the desk, so the columns stay nullable.
    //
    // The amount is stored, not recomputed on read: the tariff version can be retired and the price
    // list can change between booking and seating, and the player agreed to this number.
    public Guid? TariffVersionId { get; set; }

    public long? EstimatedCostMinorUnits { get; set; }

    public string? CurrencyCode { get; set; }
}
