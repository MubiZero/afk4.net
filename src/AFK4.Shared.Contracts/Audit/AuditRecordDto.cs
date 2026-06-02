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

    public long? AmountMinorUnits { get; init; }
}
