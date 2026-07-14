namespace AFK4.Platform.Api.Audit;

public interface IAuditRecordStager
{
    void Stage(AuditRecordWriteRequest request);
}
