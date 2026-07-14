using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordStager(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IAuditRecordStager
{
    public void Stage(AuditRecordWriteRequest request)
    {
        dbContext.AuditRecords.Add(AuditRecordFactory.Create(request, timeProvider.GetUtcNow()));
    }
}
