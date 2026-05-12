namespace AFK4.Platform.Api.Audit;

public interface IAuditRecordWriter
{
    Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken);
}
