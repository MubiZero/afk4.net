import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Tabs } from '@afk4/ui/react';
import type { OperatorAuthSession } from '../authClient';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { visibleCashTabs, type CashTab } from './cashModel';
import { CashShiftHeader } from './CashShiftHeader';
import { CashSalesWorkspace } from './CashSalesWorkspace';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import { CashOperationsLedger } from './CashOperationsLedger';
import { CashReceiptsLedger } from './CashReceiptsLedger';
import { CashTopUpRequests } from './CashTopUpRequests';
import { ReviewWorkspace } from '../ReviewWorkspace';
import { useFeedbackToasts } from '../useFeedbackToasts';

// Раздел «Касса» = шапка раздела (деньги смены и её команды — на любой вкладке) + вкладки.
export function CashWorkspace({
  backend,
  currencyCode,
  session,
  openReceipt,
  openOrder
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  // Чек из командной палитры: касса открывается сразу на чеках, а не на той вкладке, где её
  // оставили в прошлый раз.
  openReceipt?: { receiptId: string } | null;
  // Заказ бара из палитры: лента заказов живёт в «Продажах», касса открывается там.
  openOrder?: { orderId: string } | null;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>(() => visibleCashTabs(session)[0] ?? 'sales');
  const [shiftNonce, setShiftNonce] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>({ label: '', state: 'idle' });
  useFeedbackToasts(feedback);

  const visible = new Set(visibleCashTabs(session));

  useEffect(() => {
    if (openReceipt && visible.has('receipts')) {
      setActiveTab('receipts');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openReceipt?.receiptId]);

  useEffect(() => {
    if (openOrder && visible.has('sales')) {
      setActiveTab('sales');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openOrder]);
  const allTabs: { value: CashTab; label: string }[] = [
    { value: 'sales', label: t('op.cash.sales.tab') },
    { value: 'shift', label: t('op.cash.tab.shift') },
    { value: 'topups', label: t('op.cash.topups.tab') },
    { value: 'ops', label: t('op.cash.journal.segOps') },
    { value: 'receipts', label: t('op.cash.journal.segReceipts') },
    { value: 'review', label: t('op.cash.journal.segReview') }
  ];
  const tabs = allTabs.filter((tab) => visible.has(tab.value));
  const bumpShift = () => setShiftNonce((n) => n + 1);

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader
        backend={backend}
        currencyCode={currencyCode}
        session={session}
        shiftNonce={shiftNonce}
        onShiftChanged={bumpShift}
        tabs={tabs.length > 1 ? <Tabs items={tabs} value={activeTab} onChange={setActiveTab} label={t('op.shell.navGroup.cashier')} /> : undefined}
      />
      <div className="cash-tab-content">
        {activeTab === 'sales' && (
          <CashSalesWorkspace backend={backend} currencyCode={currencyCode} session={session} openOrder={openOrder} />
        )}
        {activeTab === 'shift' && backend !== null && (
          <CashShiftWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} session={session} shiftNonce={shiftNonce} onShiftChanged={bumpShift} />
        )}
        {activeTab === 'topups' && backend !== null && (
          <CashTopUpRequests
            backend={backend}
            branchId={backend.branchId}
            currencyCode={currencyCode}
            onFeedback={setFeedback}
          />
        )}
        {/* Без бэкенда лента честно остаётся в загрузке: подсунутый клиент-заглушка с пустым ответом
            утверждал бы «операций нет» там, где их просто не у кого спросить. */}
        {activeTab === 'ops' && (
          <CashOperationsLedger backend={backend} branchId={backend?.branchId ?? ''} currencyCode={currencyCode} shiftNonce={shiftNonce} />
        )}
        {activeTab === 'receipts' && backend !== null && (
          <CashReceiptsLedger backend={backend} branchId={backend.branchId} currencyCode={currencyCode} session={session} openReceipt={openReceipt} />
        )}
        {activeTab === 'review' && <ReviewWorkspace currencyCode={currencyCode} backend={backend} />}
      </div>
    </main>
  );
}
