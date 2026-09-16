import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { visibleCashTabs } from './cashModel';
import { CashShiftHeader } from './CashShiftHeader';
import { CashTabBar, type CashTab } from './CashTabBar';
import { CashSalesWorkspace } from './CashSalesWorkspace';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import { CashJournalWorkspace } from './CashJournalWorkspace';
import { CashTopUpRequests } from './CashTopUpRequests';
import { useFeedbackToasts } from '../useFeedbackToasts';

// Единый раздел «Касса» = шапка-якорь смены (статус + командная панель) + под-вкладки.
// S1: payments+shifts слиты во вкладку «Смена» (shift); действия смены живут в шапке.
export function CashWorkspace({
  backend,
  currencyCode,
  session,
  openReceipt
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  // Чек из командной палитры: касса открывается сразу на журнале, а не на той вкладке, где её
  // оставили в прошлый раз.
  openReceipt?: { receiptId: string } | null;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>(() => visibleCashTabs(session)[0] ?? 'sales');
  const [shiftNonce, setShiftNonce] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>({ label: '', state: 'idle' });
  useFeedbackToasts(feedback);

  const visible = new Set(visibleCashTabs(session));

  useEffect(() => {
    if (openReceipt && visible.has('journal')) {
      setActiveTab('journal');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openReceipt?.receiptId]);
  const allTabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.cash.sales.tab') },
    { id: 'shift', label: t('op.cash.tab.shift') },
    { id: 'topups', label: t('op.cash.topups.tab') },
    { id: 'journal', label: t('op.cash.journal.tab') }
  ];
  const tabs = allTabs.filter((tab) => visible.has(tab.id));

  return (
    <main className="workspace-screen cash-screen">
      {activeTab !== 'shift' && <CashShiftHeader
        backend={backend}
        currencyCode={currencyCode}
        session={session}
        shiftNonce={shiftNonce}
        onShiftChanged={() => setShiftNonce((n) => n + 1)}
      />}
      <CashTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} label={t('op.shell.navGroup.cashier')} />
      <div className="cash-tab-content">
        {activeTab === 'sales' && <CashSalesWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
        {activeTab === 'shift' && backend !== null && (
          <CashShiftWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} session={session} shiftNonce={shiftNonce} onShiftChanged={() => setShiftNonce((n) => n + 1)} />
        )}
        {activeTab === 'topups' && backend !== null && (
          <CashTopUpRequests
            backend={backend}
            branchId={backend.branchId}
            currencyCode={currencyCode}
            onFeedback={setFeedback}
          />
        )}
        {activeTab === 'journal' && (
          <CashJournalWorkspace
            backend={backend}
            currencyCode={currencyCode}
            session={session}
            openReceipt={openReceipt}
          />
        )}
      </div>
    </main>
  );
}
