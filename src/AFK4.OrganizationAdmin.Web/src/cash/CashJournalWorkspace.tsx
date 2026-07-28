import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { CashOperationsLedger } from './CashOperationsLedger';
import { CashReceiptsLedger } from './CashReceiptsLedger';
import { ReviewWorkspace } from '../ReviewWorkspace';
import { visibleCashJournalSegments, type CashJournalSegment } from './cashTerminalModel';

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
  const visibleSegments = visibleCashJournalSegments(session);
  const canOps = visibleSegments.includes('ops');
  const canReceipts = visibleSegments.includes('receipts');
  const canReview = visibleSegments.includes('review');
  const labels: Record<CashJournalSegment, string> = {
    ops: t('op.cash.journal.segOps'),
    receipts: t('op.cash.journal.segReceipts'),
    review: t('op.cash.journal.segReview')
  };
  const segments = visibleSegments.map((id) => ({ id, label: labels[id] }));
  const [active, setActive] = useState<CashJournalSegment>(() => visibleSegments[0] ?? 'ops');

  return (
    <main className="workspace-screen cash-journal-screen">
      {segments.length > 0 && (
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
