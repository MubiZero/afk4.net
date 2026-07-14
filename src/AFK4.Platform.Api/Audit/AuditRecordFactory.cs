using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

internal static class AuditRecordFactory
{
    public static AuditRecordEntity Create(
        AuditRecordWriteRequest request,
        DateTimeOffset createdAtUtc) =>
        new()
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            ActorStaffUserId = request.ActorStaffUserId,
            ActorPlatformAdminUserId = request.ActorPlatformAdminUserId,
            Action = request.Action,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Outcome = request.Outcome,
            SourceApp = request.SourceApp,
            DetailsJson = request.DetailsJson,
            AmountMinorUnits = request.AmountMinorUnits,
            CreatedAtUtc = createdAtUtc
        };
}
