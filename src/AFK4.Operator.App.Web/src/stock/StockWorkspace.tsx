import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { StockTabBar } from './StockTabBar';
import { StockLevelsWorkspace } from './StockLevelsWorkspace';
import { visibleStockTabs, type StockTab } from './stockModel';

const TAB_LABELS: Record<StockTab, MessageKey> = { levels: 'op.stock.tab.levels' };

// Раздел «Склад» — шапка-якорь + вкладки + активное содержимое.
// S0: одна вкладка «Остатки» → полоска скрыта (tabs.length > 1). Вернётся в S1.
export function StockWorkspace({
  currencyCode,
  backend,
  session
}: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const visible = visibleStockTabs(session);
  const [activeTab, setActiveTab] = useState<StockTab>(() => visible[0] ?? 'levels');
  const tabs = visible.map((id) => ({ id, labelKey: TAB_LABELS[id] }));

  return (
    <main className="workspace-screen stock-screen">
      <div className="cash-head">
        <h1>
          <span className="cash-head-name">{t('op.stock.title')}</span>
        </h1>
      </div>
      {tabs.length > 1 && <StockTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} />}
      <div className="cash-tab-content">
        {activeTab === 'levels' && (
          <StockLevelsWorkspace backend={backend} currencyCode={currencyCode} session={session} />
        )}
      </div>
    </main>
  );
}
