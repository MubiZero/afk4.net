import { useState } from 'react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { StockHeader } from './StockHeader';
import { StockTabBar } from './StockTabBar';
import { StockLevelsWorkspace } from './StockLevelsWorkspace';
import { ReceivingWorkspace } from './ReceivingWorkspace';
import { JournalWorkspace } from './JournalWorkspace';
import { InventoryWorkspace } from './InventoryWorkspace';
import { visibleStockTabs, type StockTab } from './stockModel';

const TAB_LABELS: Record<StockTab, MessageKey> = {
  levels: 'op.stock.tab.levels',
  receiving: 'op.stock.tab.receiving',
  journal: 'op.stock.tab.journal',
  inventory: 'op.stock.tab.inventory',
};

// Раздел «Склад» — шапка-якорь с метриками + вкладки + активное содержимое.
//
// Вкладки держим ЖИВЫМИ (lazy keep-alive): открытая вкладка монтируется один раз и дальше прячется
// через `hidden`, а не размонтируется. Иначе каждое переключение перезагружало каталог и тело
// мигало пустым каркасом. Свежесть данных держит stockNonce: он бампается после
// приёмки/инвентаризации/списания (`onStockChanged`) и прокинут во все вкладки как refreshNonce —
// тогда обновление происходит после операции, а не на каждом переключении.
export function StockWorkspace({
  currencyCode,
  backend,
  session,
}: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  const visible = visibleStockTabs(session);
  const firstTab = visible[0] ?? 'levels';
  const [activeTab, setActiveTab] = useState<StockTab>(firstTab);
  const [mounted, setMounted] = useState<StockTab[]>([firstTab]);
  const [receivePreload, setReceivePreload] = useState<{ productId: string } | null>(null);
  const [stockNonce, setStockNonce] = useState(0);
  const bumpStock = () => setStockNonce((n) => n + 1);
  const tabs = visible.map((id) => ({ id, labelKey: TAB_LABELS[id] }));

  const selectTab = (id: StockTab) => {
    setMounted((prev) => (prev.includes(id) ? prev : [...prev, id]));
    setActiveTab(id);
  };

  // Переход «оформить приёмку» с Остатков: смонтировать/показать Приёмку + (опц.) преподставить товар.
  const goToReceiving = (productId?: string) => {
    if (!visible.includes('receiving')) return;
    setReceivePreload(productId ? { productId } : null);
    selectTab('receiving');
  };

  const renderTab = (id: StockTab) => {
    switch (id) {
      case 'levels':
        return (
          <StockLevelsWorkspace
            backend={backend}
            currencyCode={currencyCode}
            session={session}
            onReceive={visible.includes('receiving') ? goToReceiving : undefined}
            onStockChanged={bumpStock}
            refreshNonce={stockNonce}
          />
        );
      case 'receiving':
        return (
          <ReceivingWorkspace
            backend={backend}
            currencyCode={currencyCode}
            session={session}
            preload={receivePreload}
            onConsumePreload={() => setReceivePreload(null)}
            onStockChanged={bumpStock}
            refreshNonce={stockNonce}
            active={activeTab === 'receiving'}
          />
        );
      case 'journal':
        return <JournalWorkspace backend={backend} currencyCode={currencyCode} session={session} refreshNonce={stockNonce} />;
      case 'inventory':
        return (
          <InventoryWorkspace
            backend={backend}
            currencyCode={currencyCode}
            session={session}
            onStockChanged={bumpStock}
            refreshNonce={stockNonce}
            active={activeTab === 'inventory'}
          />
        );
    }
  };

  return (
    <main className="workspace-screen stock-screen">
      <StockHeader backend={backend} currencyCode={currencyCode} session={session} stockNonce={stockNonce} />
      {tabs.length > 1 && <StockTabBar tabs={tabs} activeTab={activeTab} onSelect={selectTab} />}
      <div className="cash-tab-content">
        {visible
          .filter((id) => mounted.includes(id))
          .map((id) => (
            <div key={id} className="stock-tabpanel" hidden={activeTab !== id}>
              {renderTab(id)}
            </div>
          ))}
      </div>
    </main>
  );
}
