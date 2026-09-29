import type { ComponentProps, ReactNode } from 'react';
import { cx } from './cx';

// Фильтр списка: «Все 12», «Есть долг 3». Нажатое состояние — aria-pressed, а не только цвет:
// диктор читает «нажата», и выбор виден без зрения на оттенок.
export function FilterChip({ label, count, pressed, className, type = 'button', ...props }: Omit<ComponentProps<'button'>, 'children' | 'aria-pressed'> & {
  label: string;
  count?: ReactNode;
  pressed: boolean;
}) {
  return (
    <button type={type} className={cx('ui-chip', 'ui-chip--filter', className)} aria-pressed={pressed} {...props}>
      {label}
      {count !== undefined ? <b className="ui-num">{count}</b> : null}
    </button>
  );
}

// Счётчик в шапке раздела: подпись и значение. `tone` добавляет точку внимания — тревога (critical)
// или предупреждение (warning); всё остальное — спокойный счётчик без цвета.
export interface CountChipProps {
  label: string;
  value: ReactNode;
  tone?: 'critical' | 'warning';
}

export function CountChip({ label, value, tone }: CountChipProps) {
  return (
    <span className={cx('ui-chip', 'ui-chip--count', tone && `is-${tone}`)}>
      <span>{label}</span>
      <strong>{value}</strong>
    </span>
  );
}

// Статус записи: «Активна», «Долг», «Нет связи». Тонов пять, и это словарь, а не палитра: новый
// цвет статуса — это новое значение здесь, а не ещё один класс на экране.
export type StatusTone = 'success' | 'accent' | 'warning' | 'danger' | 'neutral';

const TONE: Record<StatusTone, string> = {
  success: 'is-live',
  accent: 'is-booking',
  warning: 'is-warning',
  danger: 'is-danger',
  neutral: 'is-neutral',
};

export function StatusBadge({ tone, className, ...props }: ComponentProps<'span'> & { tone: StatusTone }) {
  return <span className={cx('ui-chip', 'ui-chip--xs', 'ui-chip--status', TONE[tone], className)} {...props} />;
}
