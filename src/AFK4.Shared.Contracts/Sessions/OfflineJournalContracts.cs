namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Запись журнала кассы, который Панель вела без интернета (docs/roadmap/offline-front-desk.md). Одна
/// запись — одна операция у стойки: посадить гостя, продлить, закончить. Деньги — как их взяли
/// на самом деле, время — когда это было.
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
    string? CurrencyCode = null);

public sealed record OfflineJournalUploadRequest(IReadOnlyList<OfflineJournalRecordDto> Records);

/// <summary>Что сервер сделал с записью: принял, уже принимал раньше или отказал (с кодом).</summary>
public sealed record OfflineJournalRecordResultDto(string Id, string Status, string? Code);

public sealed record OfflineJournalUploadResponse(IReadOnlyList<OfflineJournalRecordResultDto> Results);

public static class OfflineJournalKindNames
{
    public const string SessionStarted = "session_started";
    public const string SessionExtended = "session_extended";
    public const string SessionEnded = "session_ended";
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
}
