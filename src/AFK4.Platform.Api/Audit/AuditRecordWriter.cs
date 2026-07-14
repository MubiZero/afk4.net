using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordWriter(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IAuditRecordWriter
{
    public async Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(AuditRecordFactory.Create(request, timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
