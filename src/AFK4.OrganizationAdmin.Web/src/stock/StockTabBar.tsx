import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { StockTab } from './stockModel';

// Полоса вкладок раздела «Склад». Переиспользует CSS-классы cash-tabs/cash-tab —
// единый визуальный язык вкладок раздела оператора.
export function StockTabBar({
  tabs,
  activeTab,
  onSelect
}: {
  tabs: { id: StockTab; labelKey: MessageKey }[];
  activeTab: StockTab;
  onSelect: (tab: StockTab) => void;
}) {
  const { t } = useI18n();
  return (
    <div className="cash-tabs" role="tablist">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={tab.id === activeTab}
          className={`cash-tab${tab.id === activeTab ? ' active' : ''}`}
          onClick={() => onSelect(tab.id)}
        >
          {t(tab.labelKey)}
        </button>
      ))}
    </div>
  );
}
