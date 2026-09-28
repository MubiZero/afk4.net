namespace AFK4.Shared.Contracts.Audit;

public sealed record AuditRecordDto(
    Guid AuditRecordId,
    Guid OrganizationId,
    Guid? BranchId,
    Guid? ActorStaffUserId,
    string Action,
    string TargetType,
    string? TargetId,
    string Outcome,
    string SourceApp,
    string DetailsJson,
    DateTimeOffset CreatedAtUtc)
{
    public Guid? ActorPlatformAdminUserId { get; init; }

    /// <summary>Имя клуба-клиента, к которому относится запись. Панель платформы смотрит журнал
    /// поверх всей сети, и опознавательный знак «кто» там — имя, а не идентификатор: наизусть их
    /// не знает никто. Пусто, если организация к моменту чтения журнала уже удалена.</summary>
    public string? OrganizationName { get; init; }

    public long? AmountMinorUnits { get; init; }

    /// <summary>Кто сделал — по имени: сотрудник клуба, сотрудник платформы или служебный
    /// исполнитель. Журнал открывают, чтобы ответить «кто трогал подписку», и столбец GUID на этот
    /// вопрос не отвечает. Пусто — действие системы без исполнителя.</summary>
    public string? ActorDisplayName { get; init; }
}
