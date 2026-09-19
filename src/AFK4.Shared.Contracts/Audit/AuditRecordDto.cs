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
}
