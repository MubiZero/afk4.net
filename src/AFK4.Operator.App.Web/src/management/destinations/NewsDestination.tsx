import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { NewsWorkspace } from '../../NewsWorkspace';
import type { DestinationProps } from './types';

// Новости: MgmtTable + MgmtDrawer (create+edit), как Товары. NewsWorkspace несёт свою форму
// create/update с собственной кнопкой сохранения в футере дровера — save-бар ManagementScreen не
// нужен. Полный dirty-tracking уточняется отдельно; пока unsaved guard не блокирует уход отсюда.
export function NewsDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen title={t('op.management.dest.news')} subtitle={t('op.management.dest.news.subtitle')} contentWidth="full">
      <NewsWorkspace backend={backend} />
    </ManagementScreen>
  );
}
