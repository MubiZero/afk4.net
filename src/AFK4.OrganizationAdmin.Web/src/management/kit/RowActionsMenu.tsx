import { useI18n } from '@afk4/i18n';
import { RowActions } from '@afk4/ui/react';
import type { RowAction } from './types';

// Меню «⋯» строки/записи — RowActions кита (клавиатура, фокус, опасное за разделителем живут там).
// Здесь только подпись по умолчанию из каталога Панели.
export function RowActionsMenu({ actions, ariaLabel, size }: { actions: RowAction[]; ariaLabel?: string; size?: 'sm' | 'md' }) {
  const { t } = useI18n();
  return <RowActions actions={actions} label={ariaLabel ?? t('op.management.crud.rowMenu')} size={size} />;
}
