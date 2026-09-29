import { useRef } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import { cx } from './cx';

// Стрелки двигают выбор по кругу, Home/End — к краям. Выбор идёт сразу за фокусом (автоматическая
// активация по APG): вкладки и сегменты здесь дешёвые, переключение не грузит ничего тяжёлого.
function rovingKeys<T>(event: KeyboardEvent, values: T[], current: T, pick: (value: T, index: number) => void) {
  const index = values.indexOf(current);
  const last = values.length - 1;
  const targets: Record<string, number> = {
    ArrowRight: index >= last ? 0 : index + 1,
    ArrowLeft: index <= 0 ? last : index - 1,
    Home: 0,
    End: last,
  };
  const target = targets[event.key];
  if (target === undefined) return;
  event.preventDefault();
  pick(values[target], target);
}

export interface TabItem<T extends string> {
  value: T;
  label: string;
  /** Число рядом с подписью — «Долги 3». Тихим цветом, чтобы не спорить с подписью. */
  count?: ReactNode;
}

// Вкладки страницы — подчёркиванием. Меняют то, что показано ниже, поэтому управляются снаружи:
// активная вкладка живёт в URL или состоянии экрана, а не внутри виджета. В порядке табуляции —
// только выбранная (roving tabindex): Tab проходит полосу вкладок одним шагом, а не девятью.
export function Tabs<T extends string>({ items, value, onChange, label, className }: {
  items: TabItem<T>[];
  value: T;
  onChange: (next: T) => void;
  label: string;
  className?: string;
}) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const values = items.map((item) => item.value);
  // Ни одна не выбрана (значение ещё грузится) — в табуляцию встаёт первая, иначе полоса недостижима.
  const entry = values.includes(value) ? value : values[0];
  return (
    <div className={cx('ui-tabs', className)} role="tablist" aria-label={label}>
      {items.map((item, index) => {
        const selected = item.value === value;
        return (
          <button
            key={item.value}
            ref={(node) => { refs.current[index] = node; }}
            type="button"
            role="tab"
            className="ui-tab"
            aria-selected={selected}
            tabIndex={item.value === entry ? 0 : -1}
            onClick={() => onChange(item.value)}
            onKeyDown={(event) => rovingKeys(event, values, value, (next, at) => {
              refs.current[at]?.focus();
              onChange(next);
            })}
          >
            {item.label}
            {item.count !== undefined ? <span className="ui-tab-count">{item.count}</span> : null}
          </button>
        );
      })}
    </div>
  );
}

export interface SegmentOption<T extends string> {
  value: T;
  label: string;
  disabled?: boolean;
}

// Сегмент — выбор одного значения внутри формы или панели («Наличные / Карта»). Это не навигация,
// а поле: role=radiogroup, и значение читается как отмеченное (aria-checked), а не как «открытая
// вкладка».
export function Segmented<T extends string>({ options, value, onChange, label, className }: {
  options: SegmentOption<T>[];
  value: T;
  onChange: (next: T) => void;
  label: string;
  className?: string;
}) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const enabled = options.filter((option) => !option.disabled).map((option) => option.value);
  const entry = enabled.includes(value) ? value : enabled[0];
  return (
    <div className={cx('ui-segmented', className)} role="radiogroup" aria-label={label}>
      {options.map((option, index) => {
        const checked = option.value === value;
        return (
          <button
            key={option.value}
            ref={(node) => { refs.current[index] = node; }}
            type="button"
            role="radio"
            className="ui-segment"
            aria-checked={checked}
            tabIndex={option.value === entry ? 0 : -1}
            disabled={option.disabled}
            onClick={() => onChange(option.value)}
            onKeyDown={(event) => rovingKeys(event, enabled, value, (next) => {
              refs.current[options.findIndex((candidate) => candidate.value === next)]?.focus();
              onChange(next);
            })}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
