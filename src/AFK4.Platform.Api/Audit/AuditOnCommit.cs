namespace AFK4.Platform.Api.Audit;

/// <summary>
/// Записи журнала, которые денежная команда ставит в свою транзакцию вместе с деньгами: ответ
/// команды → записи. Раньше точка входа писала журнал отдельным сохранением уже после коммита, и
/// падение между ними оставляло деньги без записи — ровно там, где журнал нужнее всего.
/// </summary>
public delegate IEnumerable<AuditRecordWriteRequest> AuditOnCommit<in TResponse>(TResponse response);

public static class AuditOnCommitStaging
{
    /// <summary>
    /// Ставит записи в текущую транзакцию команды: её следующее сохранение закоммитит их вместе с
    /// деньгами. Запросили запись, а ставить нечем, — ошибка, а не тихо потерянный журнал.
    /// </summary>
    public static void StageOnCommit<TResponse>(
        this IAuditRecordStager? stager,
        AuditOnCommit<TResponse>? auditOnCommit,
        TResponse response)
    {
        if (auditOnCommit is null)
        {
            return;
        }

        var required = Require(stager);
        foreach (var audit in auditOnCommit(response))
        {
            required.Stage(audit);
        }
    }

    /// <summary>Одна запись, которой ответ команды не нужен.</summary>
    public static void StageOnCommit(this IAuditRecordStager? stager, AuditRecordWriteRequest? audit)
    {
        if (audit is not null)
        {
            Require(stager).Stage(audit);
        }
    }

    private static IAuditRecordStager Require(IAuditRecordStager? stager) =>
        stager ?? throw new InvalidOperationException("A money command was asked to write audit records but has no audit stager.");
}
