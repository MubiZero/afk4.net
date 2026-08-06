import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { NewsWorkspace } from '../../NewsWorkspace';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import type { DestinationProps } from './types';

// Новости: MgmtTable + MgmtDrawer (create+edit), как Товары. NewsWorkspace несёт свою форму
// create/update с собственной кнопкой сохранения в футере дровера — save-бар ManagementScreen не
// нужен. Полный dirty-tracking уточняется отдельно; пока unsaved guard не блокирует уход отсюда.
//
// canManage — по образцу GoodsDestination.canManagePosCatalog/PaymentsLoyaltyDestination.canGateways:
// эта вкладка — единственная в «Управлении», где до сих пор не было отдельного client-side гейта на
// запись (только серверный 403). Под обычным сотрудником hasPermission(manageNews) 1:1 совпадает с
// тем, что реально разрешит сервер (одно и то же право на все 4 операции) — гейт тут страховка на
// будущее, а не текущий баг. Под гранта поддержки список чтения (GET news) и список записи
// (POST/PATCH/DELETE) на сервере размечены РАЗНО (см. NewsEndpoints.cs — только GET помечен
// AllowPlatformSupportAccess), а support/supportWorkspaces.ts сознательно не выдаёт manageNews вовсе
// (нет соответствующей writable area) — так что под поддержкой canManage будет false и раздел вообще
// не появится в списке (managementNav.ts гейтит саму вкладку тем же правом). Гейт всё равно добавлен
// явно, а не оставлен «раз тут просто нечего показывать»: то же самое currentTarget-условие защищает
// и гипотетический будущий сценарий частичного права.
export function NewsDestination({ backend, session, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canManage = hasPermission(session, permissionNames.manageNews);

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen title={t('op.management.dest.news')} subtitle={t('op.management.dest.news.subtitle')} contentWidth="full">
      <NewsWorkspace backend={backend} canManage={canManage} />
    </ManagementScreen>
  );
}
