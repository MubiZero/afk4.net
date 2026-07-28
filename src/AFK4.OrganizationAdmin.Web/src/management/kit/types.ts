import type { ReactNode } from 'react';

// Одно действие в ⋯-меню строки/дровера. `danger` рисует пункт красным и (если перед ним есть
// обычные пункты) отделяет разделителем — опасное действие визуально отбито от рутинных.
export interface RowAction {
  id: string;
  label: string;
  icon?: ReactNode;
  onSelect: () => void;
  danger?: boolean;
  disabled?: boolean;
}

// Колонка списка. `render` возвращает содержимое ячейки для строки типа T. `align: 'end'` —
// правое выравнивание (числа/деньги/статусы), заголовок выравнивается так же.
export interface MgmtColumn<T> {
  key: string;
  header: string;
  align?: 'start' | 'end';
  render: (row: T) => ReactNode;
}
