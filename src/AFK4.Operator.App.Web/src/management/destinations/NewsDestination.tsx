import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { NewsWorkspace } from '../../NewsWorkspace';
import type { DestinationProps } from './types';

// Новости: слайс 1 просто монтирует существующий NewsWorkspace без save-бара ManagementScreen —
// у него своя форма create/update с собственной кнопкой сохранения. Полный dirty-tracking
// уточняется в Task 1.7 вместе с Лояльностью; пока unsaved guard не блокирует уход отсюда.
export function NewsDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen title={t('op.management.dest.news')} subtitle={t('op.management.dest.news.subtitle')}>
      <div className="management-panel">
        <NewsWorkspace backend={backend} />
      </div>
    </ManagementScreen>
  );
}
