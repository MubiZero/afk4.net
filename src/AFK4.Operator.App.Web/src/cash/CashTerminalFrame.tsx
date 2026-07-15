import { useRef, type CSSProperties, type KeyboardEvent, type ReactNode } from 'react';
import { X } from 'lucide-react';

export interface CashMetricItem {
  label: string;
  value: ReactNode;
  tone?: 'default' | 'positive' | 'attention' | 'danger';
  detail?: string;
}

export function CashMetricStrip({ items, ariaLabel }: { items: CashMetricItem[]; ariaLabel: string }) {
  return (
    <section className="cash-terminal-metrics" aria-label={ariaLabel} style={{ '--cash-metric-count': items.length } as CSSProperties}>
      {items.map((item) => (
        <div key={item.label} className={`cash-terminal-metric tone-${item.tone ?? 'default'}`}>
          <span>{item.label}</span>
          <strong>{item.value}</strong>
          {item.detail ? <small>{item.detail}</small> : null}
        </div>
      ))}
    </section>
  );
}

export function CashTerminalSplit({
  register,
  inspector,
  inspectorOpen,
  closeLabel,
  onCloseInspector
}: {
  register: ReactNode;
  inspector: ReactNode;
  inspectorOpen: boolean;
  closeLabel: string;
  onCloseInspector: () => void;
}) {
  return (
    <div className={`cash-terminal-split${inspectorOpen ? ' inspector-open' : ''}`}>
      <section className="cash-terminal-register">{register}</section>
      <aside className="cash-terminal-inspector" aria-label="Детали выбранной записи">
        <button type="button" className="cash-inspector-close" aria-label={closeLabel} onClick={onCloseInspector}>
          <X size={16} aria-hidden="true" />
        </button>
        {inspector}
      </aside>
    </div>
  );
}

export function CashRegisterRows<TRow>({
  rows,
  selectedId,
  getId,
  renderRow,
  onSelect,
  ariaLabel
}: {
  rows: TRow[];
  selectedId: string;
  getId: (row: TRow) => string;
  renderRow: (row: TRow) => ReactNode;
  onSelect: (id: string) => void;
  ariaLabel: string;
}) {
  const rowRefs = useRef<Array<HTMLDivElement | null>>([]);
  const selectedIndex = rows.findIndex((row) => getId(row) === selectedId);
  const tabbableIndex = selectedIndex >= 0 ? selectedIndex : 0;

  const moveSelection = (event: KeyboardEvent<HTMLDivElement>, index: number) => {
    let nextIndex = index;
    if (event.key === 'ArrowDown') nextIndex = Math.min(rows.length - 1, index + 1);
    else if (event.key === 'ArrowUp') nextIndex = Math.max(0, index - 1);
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = rows.length - 1;
    else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onSelect(getId(rows[index]));
      return;
    } else return;

    event.preventDefault();
    const nextId = getId(rows[nextIndex]);
    onSelect(nextId);
    rowRefs.current[nextIndex]?.focus();
  };

  return (
    <div className="cash-register-rows" role="table" aria-label={ariaLabel}>
      {rows.map((row, index) => {
        const id = getId(row);
        return (
          <div
            key={id}
            ref={(node) => { rowRefs.current[index] = node; }}
            className="cash-register-row"
            role="row"
            tabIndex={index === tabbableIndex ? 0 : -1}
            aria-selected={id === selectedId}
            onClick={() => onSelect(id)}
            onKeyDown={(event) => moveSelection(event, index)}
          >
            {renderRow(row)}
          </div>
        );
      })}
    </div>
  );
}
