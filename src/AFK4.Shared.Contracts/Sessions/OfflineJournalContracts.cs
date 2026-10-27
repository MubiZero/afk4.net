namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Запись журнала кассы, который Панель вела без интернета (docs/roadmap/offline-front-desk.md). Одна
/// запись — одна операция у стойки: посадить гостя, продлить, закончить, продать товар, закрыть или
/// открыть смену. Деньги — как их взяли на самом деле, время — когда это было.
/// <para>
/// <see cref="SessionId"/> — только у записей о сессии. <see cref="ChargeMinorUnits"/> — сколько взяли с
/// гостя (за время или за товар). <see cref="CashMinorUnits"/> — наличные в ящике: посчитанные при
/// закрытии смены или размен при открытии. <see cref="ShiftId"/> у открытия — номер новой смены,
/// придуманный Панелью: следующие записи кладут деньги в неё.
/// </para>
/// </summary>
public sealed record OfflineJournalRecordDto(
    string Id,
    string Kind,
    Guid SessionId,
    DateTimeOffset OccurredAtUtc,
    Guid ShiftId,
    Guid? SeatId = null,
    Guid? DeviceId = null,
    string? TariffRuleVersionId = null,
    int Minutes = 0,
    long ChargeMinorUnits = 0,
    string? CurrencyCode = null,
    IReadOnlyList<OfflineSaleLineDto>? Lines = null,
    long CashMinorUnits = 0,
    string? Note = null);

/// <summary>Товар, проданный без связи: цена — та, по которой взяли деньги (из кэша Панели).</summary>
public sealed record OfflineSaleLineDto(Guid ProductId, int Quantity, long UnitPriceMinorUnits);

public sealed record OfflineJournalUploadRequest(IReadOnlyList<OfflineJournalRecordDto> Records);

/// <summary>Что сервер сделал с записью: принял, уже принимал раньше или отказал (с кодом).</summary>
public sealed record OfflineJournalRecordResultDto(string Id, string Status, string? Code);

public sealed record OfflineJournalUploadResponse(IReadOnlyList<OfflineJournalRecordResultDto> Results);

public static class OfflineJournalKindNames
{
    public const string SessionStarted = "session_started";
    public const string SessionExtended = "session_extended";
    public const string SessionEnded = "session_ended";
    public const string Sale = "sale";
    public const string ShiftClosed = "shift_closed";
    public const string ShiftOpened = "shift_opened";
}

public static class OfflineJournalStatusNames
{
    public const string Applied = "applied";
    public const string Duplicate = "duplicate";

    /// <summary>
    /// Отказ окончательный: повтор не поможет. Запись всё равно хранится у сервера целиком — деньги,
    /// взятые без связи, не теряются, их видит владелец.
    /// </summary>
    public const string Rejected = "rejected";
}

public static class OfflineJournalRejectionNames
{
    /// <summary>Смену, в которую брали деньги, уже закрыли: задним числом в неё не пишем.</summary>
    public const string ShiftClosed = "offline_shift_closed";

    /// <summary>Место, ПК, смена или сессия — не из этого филиала или их нет.</summary>
    public const string UnknownTarget = "offline_unknown_target";

    /// <summary>Запись не по правилам: вид, минуты, сумма, валюта.</summary>
    public const string Invalid = "offline_record_invalid";

    /// <summary>У того, кто досылает журнал, нет права на эту операцию (продать, открыть или закрыть смену).</summary>
    public const string NotAllowed = "offline_not_allowed";

    /// <summary>Открыть смену без связи нельзя: в филиале уже открыта другая.</summary>
    public const string ShiftAlreadyOpen = "offline_shift_already_open";
}
