import type { ReactNode } from 'react';

// Сегментный переключатель разделов (.mgmt-tabs) — тот же контрол, что во «Управлении»
// Organization Admin. Управляется снаружи: активная вкладка живёт в URL, а не внутри виджета.
export interface TabItem<T extends string> {
  value: T;
  label: string;
}

export function Tabs<T extends string>({ items, value, onChange, label }: {
  items: TabItem<T>[];
  value: T;
  onChange: (next: T) => void;
  label: string;
}) {
  return (
    <div className="mgmt-tabs" role="tablist" aria-label={label}>
      {items.map(item => (
        <button
          key={item.value}
          type="button"
          role="tab"
          aria-selected={item.value === value}
          className={item.value === value ? 'mgmt-tab is-active' : 'mgmt-tab'}
          onClick={() => onChange(item.value)}
        >
          {item.label}
        </button>
      ))}
    </div>
  );
}

export function TabPanel({ children }: { children: ReactNode }) {
  return <div role="tabpanel">{children}</div>;
}
