namespace AFK4.Platform.Api.Audit;

public sealed record AuditRecordWriteRequest(
    Guid OrganizationId,
    Guid? BranchId,
    Guid? ActorStaffUserId,
    string Action,
    string TargetType,
    string? TargetId,
    string Outcome,
    string SourceApp,
    string DetailsJson);
