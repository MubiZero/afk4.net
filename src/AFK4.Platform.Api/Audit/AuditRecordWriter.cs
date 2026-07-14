using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordWriter(
    PlatformDbContext dbContext,
    IAuditRecordStager auditRecordStager) : IAuditRecordWriter
{
    public async Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
    {
        auditRecordStager.Stage(request);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
