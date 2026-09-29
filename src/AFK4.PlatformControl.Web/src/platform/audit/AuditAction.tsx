import { useI18n } from '@/i18n/I18nProvider';
import { auditActionLabel } from './auditModel';

/// Действие журнала словами, а под ним — код мелко: по коду ищут в фильтре «Действие» и в логах.
/// Незнакомый код — как есть: выдумывать за сервер название события нельзя.
export function AuditAction({ action }: { action: string }) {
  const { t } = useI18n();
  const label = auditActionLabel(action, t);
  return label === null ? <code className="ui-code">{action}</code> : (
    <span className="pc-audit-action">
      <span>{label}</span>
      <code className="ui-code">{action}</code>
    </span>
  );
}
