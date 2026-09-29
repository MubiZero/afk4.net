import type { ReactNode } from 'react';

// Одно действие в ⋯-меню строки/дровера — тип кита (@afk4/ui/react).
export type { RowAction } from '@afk4/ui/react';

// Колонка списка. `render` возвращает содержимое ячейки для строки типа T. `align: 'end'` —
// правое выравнивание (числа/деньги/статусы), заголовок выравнивается так же.
export interface MgmtColumn<T> {
  key: string;
  header: string;
  align?: 'start' | 'end';
  render: (row: T) => ReactNode;
}
