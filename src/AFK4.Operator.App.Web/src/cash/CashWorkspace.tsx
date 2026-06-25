import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import type { OperatorBackendContext } from '../operatorTypes';
import { visibleCashTabs } from './cashModel';
import { CashShiftHeader } from './CashShiftHeader';
import { CashTabBar, type CashTab } from './CashTabBar';
import { CashSalesWorkspace } from './CashSalesWorkspace';
import { CashStockWorkspace } from './CashStockWorkspace';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import { CashJournalWorkspace } from './CashJournalWorkspace';

// Единый раздел «Касса» = шапка-якорь смены (статус + командная панель) + под-вкладки.
// S1: payments+shifts слиты во вкладку «Смена» (shift); действия смены живут в шапке.
export function CashWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>(() => visibleCashTabs(session)[0] ?? 'sales');
  const [shiftNonce, setShiftNonce] = useState(0);

  const visible = new Set(visibleCashTabs(session));
  const allTabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.cash.sales.tab') },
    { id: 'stock', label: t('op.cash.stock.tab') },
    { id: 'shift', label: t('op.cash.tab.shift') },
    { id: 'journal', label: t('op.cash.journal.tab') }
  ];
  const tabs = allTabs.filter((tab) => visible.has(tab.id));

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader
        backend={backend}
        currencyCode={currencyCode}
        session={session}
        shiftNonce={shiftNonce}
        onShiftChanged={() => setShiftNonce((n) => n + 1)}
      />
      <CashTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} label={t('op.shell.navGroup.cashier')} />
      <div className="cash-tab-content">
        {activeTab === 'sales' && <CashSalesWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
        {activeTab === 'stock' && <CashStockWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
        {activeTab === 'shift' && backend !== null && (
          <CashShiftWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} shiftNonce={shiftNonce} />
        )}
        {activeTab === 'journal' && <CashJournalWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
      </div>
    </main>
  );
}
