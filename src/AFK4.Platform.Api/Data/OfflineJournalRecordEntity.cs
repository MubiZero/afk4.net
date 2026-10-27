namespace AFK4.Platform.Api.Data;

/// <summary>
/// Запись журнала кассы без интернета, как её прислала Панель, и что сервер с ней сделал. Хранится
/// целиком и при отказе: деньги, взятые без связи, не должны пропасть ни при каком исходе.
/// </summary>
public sealed class OfflineJournalRecordEntity
{
    public Guid OfflineJournalRecordId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>Идентификатор записи у Панели: по нему повтор отправки ничего не удваивает.</summary>
    public string RecordId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public Guid SessionId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? RejectionCode { get; set; }

    public Guid UploadedByStaffUserId { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
