import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import { CashShiftHeader } from './CashShiftHeader';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';
import { BackendPaymentsWorkspace } from '../BackendPaymentsWorkspace';
import { ShiftsWorkspace } from '../ShiftsWorkspace';
import { ReviewWorkspace } from '../ReviewWorkspace';

export type CashTab = 'sales' | 'orders' | 'payments' | 'shifts' | 'review';

// S0: единый раздел «Касса» = шапка-якорь смены + под-вкладки, переносящие существующие
// воркспейсы 1:1 (без слияния контента). Слияние Платежи+Смены → S1, Продажи+Заказы → S3.
export function CashWorkspace({
  backend,
  currencyCode
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>('sales');

  const tabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.shell.nav.pos') },
    { id: 'orders', label: t('op.shell.nav.shop_orders') },
    { id: 'payments', label: t('op.shell.nav.payments') },
    { id: 'shifts', label: t('op.shifts.nav') },
    { id: 'review', label: t('op.shell.nav.review') }
  ];

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader backend={backend} currencyCode={currencyCode} />
      <div className="cash-tabs" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.id}
            className={`cash-tab${activeTab === tab.id ? ' active' : ''}`}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div className="cash-tab-content">
        {activeTab === 'sales' && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} />}
        {activeTab === 'orders' && <ShopOrdersWorkspace backend={backend} />}
        {activeTab === 'payments' && <BackendPaymentsWorkspace currencyCode={currencyCode} backend={backend} />}
        {activeTab === 'shifts' && backend !== null && (
          <ShiftsWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} />
        )}
        {activeTab === 'review' && <ReviewWorkspace currencyCode={currencyCode} backend={backend} />}
      </div>
    </main>
  );
}
