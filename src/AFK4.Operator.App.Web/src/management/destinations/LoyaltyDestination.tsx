import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { LoyaltySettingsWorkspace } from '../../LoyaltySettingsWorkspace';
import type { DestinationProps } from './types';

// Лояльность: слайс 1 просто монтирует существующий LoyaltySettingsWorkspace без save-бара
// ManagementScreen — он сам грузит/сохраняет через свой собственный клиент и кнопку. Полный
// dirty-tracking (guard видит несохранённые правки процентов) уточняется в Task 1.7, когда
// save переезжает под общий scaffold; пока unsaved guard просто не блокирует уход отсюда.
export function LoyaltyDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen title={t('op.management.dest.loyalty')} subtitle={t('op.management.dest.loyalty.subtitle')}>
      <div className="management-panel">
        <LoyaltySettingsWorkspace backend={backend} />
      </div>
    </ManagementScreen>
  );
}
