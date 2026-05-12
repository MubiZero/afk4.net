using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordWriter(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IAuditRecordWriter
{
    public async Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            ActorStaffUserId = request.ActorStaffUserId,
            Action = request.Action,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Outcome = request.Outcome,
            SourceApp = request.SourceApp,
            DetailsJson = request.DetailsJson,
            CreatedAtUtc = timeProvider.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
