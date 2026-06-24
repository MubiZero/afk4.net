export type CashTab = 'sales' | 'orders' | 'payments' | 'shifts' | 'review';

// Презентационная полоса под-вкладок раздела «Касса». Вынесена из CashWorkspace, чтобы
// тестироваться изолированно — без рендера тяжёлых дочерних воркспейсов и без mock.module.
export function CashTabBar({
  tabs,
  activeTab,
  onSelect,
  label
}: {
  tabs: { id: CashTab; label: string }[];
  activeTab: CashTab;
  onSelect: (id: CashTab) => void;
  label?: string;
}) {
  return (
    <div className="cash-tabs" role="tablist" aria-label={label}>
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          role="tab"
          aria-selected={activeTab === tab.id}
          className={`cash-tab${activeTab === tab.id ? ' active' : ''}`}
          onClick={() => onSelect(tab.id)}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}
