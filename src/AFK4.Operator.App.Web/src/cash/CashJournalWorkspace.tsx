import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { CashOperationsLedger } from './CashOperationsLedger';
import { CashReceiptsLedger } from './CashReceiptsLedger';
import { ReviewWorkspace } from '../ReviewWorkspace';

type JournalSegment = 'ops' | 'receipts' | 'review';

// Вкладка «Журнал кассы» = лента кассовых операций + аппрув возвратов/коррекций (ReviewWorkspace
// во встроенном режиме). Сегменты гейтятся правами — оператор не видит сегмент без доступа.
export function CashJournalWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const canOps = hasAnyPermission(session, [permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash]);
  const canReceipts = hasAnyPermission(session, [permissionNames.viewReceipt, permissionNames.refundPosSale]);
  const canReview = hasAnyPermission(session, [permissionNames.approveMoneyAction]);

  const segments: { id: JournalSegment; label: string }[] = [];
  if (canOps) segments.push({ id: 'ops', label: t('op.cash.journal.segOps') });
  if (canReceipts) segments.push({ id: 'receipts', label: t('op.cash.journal.segReceipts') });
  if (canReview) segments.push({ id: 'review', label: t('op.cash.journal.segReview') });

  const [active, setActive] = useState<JournalSegment>(() => segments[0]?.id ?? 'ops');

  return (
    <main className="workspace-screen cash-journal-screen">
      {segments.length > 1 && (
        <div className="cash-journal-segments" role="tablist" aria-label={t('op.cash.journal.title')}>
          {segments.map((segment) => (
            <button
              key={segment.id}
              type="button"
              role="tab"
              aria-selected={active === segment.id}
              className={active === segment.id ? 'active' : undefined}
              onClick={() => setActive(segment.id)}
            >
              {segment.label}
            </button>
          ))}
        </div>
      )}

      {active === 'ops' && canOps && backend !== null && (
        <CashOperationsLedger backend={backend} branchId={backend.branchId} currencyCode={currencyCode} />
      )}
      {active === 'ops' && canOps && backend === null && (
        <CashOperationsLedger backend={null} branchId="" currencyCode={currencyCode} reports={{ getCashOperationReport: async () => ({ rows: [] }) }} />
      )}
      {active === 'receipts' && canReceipts && backend !== null && (
        <CashReceiptsLedger backend={backend} branchId={backend.branchId} currencyCode={currencyCode} session={session} />
      )}
      {active === 'review' && canReview && <ReviewWorkspace currencyCode={currencyCode} backend={backend} embedded />}
    </main>
  );
}
