import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import type { OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { CashShiftHeader } from './CashShiftHeader';
import { CashTabBar, type CashTab } from './CashTabBar';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';
import { BackendPaymentsWorkspace } from '../BackendPaymentsWorkspace';
import { ShiftsWorkspace } from '../ShiftsWorkspace';
import { ReviewWorkspace } from '../ReviewWorkspace';

// S0: единый раздел «Касса» = шапка-якорь смены + под-вкладки, переносящие существующие
// воркспейсы 1:1 (без слияния контента). Слияние Платежи+Смены → S1, Продажи+Заказы → S3.
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
  const [activeTab, setActiveTab] = useState<CashTab>('sales');
  const canReview = hasPermission(session, permissionNames.approveMoneyAction);

  const allTabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.shell.nav.pos') },
    { id: 'orders', label: t('op.shell.nav.shop_orders') },
    { id: 'payments', label: t('op.shell.nav.payments') },
    { id: 'shifts', label: t('op.shifts.nav') },
    { id: 'review', label: t('op.shell.nav.review') }
  ];
  const tabs = allTabs.filter((tab) => tab.id !== 'review' || canReview);

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader backend={backend} currencyCode={currencyCode} />
      <CashTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} label={t('op.shell.navGroup.cashier')} />
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
