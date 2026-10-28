namespace AFK4.Platform.Api.Data;

/// <summary>
/// ПК был обесточен (сообщил агент после включения): с какого по какой момент. По нескольким таким записям
/// филиала с одним началом сервер видит отключение света в клубе и возвращает гостям время
/// (docs/roadmap/power-outage-resume.md).
/// </summary>
public sealed class DevicePowerLossEntity
{
    public Guid DevicePowerLossId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    /// <summary>Когда агент последний раз был жив (часы платформы).</summary>
    public DateTimeOffset FromUtc { get; set; }

    /// <summary>Когда машина включилась: время сервера минус аптайм агента.</summary>
    public DateTimeOffset ToUtc { get; set; }

    /// <summary>Подтверждено отключение света в клубе (не один выдернутый ПК) и время по нему возвращено.</summary>
    public bool Compensated { get; set; }

    public Guid? SessionId { get; set; }

    public int ReturnedMinutes { get; set; }

    public DateTimeOffset ReportedAtUtc { get; set; }
}
