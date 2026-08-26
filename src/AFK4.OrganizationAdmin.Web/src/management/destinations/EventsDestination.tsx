import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { EventsWorkspace } from '../../EventsWorkspace';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import type { DestinationProps } from './types';

// События клуба — по образцу «Новостей»: своя форма create/update с кнопкой в футере дровера,
// save-бар ManagementScreen не нужен.
//
// canManage: право на события отдельно от новостей, потому что отмена события возвращает деньги
// всем записавшимся. Под поддержкой платформы раздел не появится вовсе (managementNav гейтит
// вкладку тем же правом, а writable area для него не выдаётся), но гейт стоит явно — то же
// условие защитит и будущий сценарий частичного права.
export function EventsDestination({ backend, session, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canManage = hasPermission(session, permissionNames.manageTournaments);

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen
      title={t('op.management.dest.events')}
      subtitle={t('op.management.dest.events.subtitle')}
      contentWidth="full"
    >
      <EventsWorkspace backend={backend} canManage={canManage} />
    </ManagementScreen>
  );
}
