namespace AFK4.Platform.Api.Audit;

/// <summary>
/// Записи журнала, которые денежная команда ставит в свою транзакцию вместе с деньгами: ответ
/// команды → записи. Раньше точка входа писала журнал отдельным сохранением уже после коммита, и
/// падение между ними оставляло деньги без записи — ровно там, где журнал нужнее всего.
/// </summary>
public delegate IEnumerable<AuditRecordWriteRequest> AuditOnCommit<in TResponse>(TResponse response);
