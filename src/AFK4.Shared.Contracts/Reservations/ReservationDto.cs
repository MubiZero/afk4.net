namespace AFK4.Shared.Contracts.Reservations;

public sealed record ReservationDto(
    Guid ReservationId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? PlayerAccountId,
    Guid? SeatId,
    string? SeatName,
    string? ZoneName,
    string CustomerName,
    string? PhoneNumber,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    int DurationMinutes,
    string State,
    string Source,
    string Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string CancelReason,
    Guid? ReservationGroupId,
    int Version = 1,
    Guid? StartedSessionId = null,
    // Billing choice carried by a self-service booking, and the price the server computed for it.
    // Null for desk-created bookings — those are still priced when the player is seated.
    Guid? TariffVersionId = null,
    string? TariffName = null,
    long? EstimatedCostMinorUnits = null,
    string? CurrencyCode = null,
    // Докуда клуб обещал ответить на заявку и когда ответил. Срок есть только у заявки, которая
    // ждёт решения стойки; подтверждённую бронь по таймеру никто не снимает.
    DateTimeOffset? RespondByUtc = null,
    DateTimeOffset? ConfirmedAtUtc = null,
    // Личность за счётом, с которого пришла заявка. Клуб, решающий её судьбу, спрашивает сеть
    // этим идентификатором, а не телефоном гостя. У заявки, записанной на стойке одним номером,
    // счёта ещё нет — и называть некого.
    Guid? PlatformPersonId = null,
    // Чем кончилась бронь, в которую человек не приехал: когда это признали и сколько филиал
    // оставил себе по своей же настройке. Сумма пустая, а не нулевая, когда не удерживали вовсе:
    // ноль читался бы как «удержали нисколько», хотя удержания не было.
    DateTimeOffset? NoShowAtUtc = null,
    long? RetainedAmountMinorUnits = null,
    // Отказ клуба: когда, по какой причине из справочника и что администратор добавил словами.
    // Причину читает игрок — поэтому код, а не текст: текст на языке стойки ему не поможет.
    DateTimeOffset? RejectedAtUtc = null,
    string? RejectReasonCode = null,
    string? RejectReasonNote = null);

public sealed record ReservationSearchResultDto(
    IReadOnlyList<ReservationDto> Reservations,
    int Limit);
