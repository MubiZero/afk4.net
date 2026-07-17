import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { isGuid, readArray, readString } from '../../operatorHelpers';
import { managementScreenState, type DestinationProps } from './types';
import { ZonesTab } from './halls/ZonesTab';
import { DevicesTab } from './halls/DevicesTab';

type HallsTab = 'layout' | 'devices';

// Залы и ПК: эталон CRUD-раздела «Управления» — список + drawer вместо вечно-развёрнутой формы
// (см. task-B2-halls-rework-brief.md). Домен A (зоны/места, ZonesTab, two-pane full width) и
// домен B (устройства/ключи, DevicesTab — provisioning живёт в Мастере настройки, lock/unlock на
// Карте) живут во внутренних вкладках раздела. Каждая операция коммитит сама — общего save-бара
// нет, поэтому dirty-guard не нужен, сигнализируем "clean" сразу на маунте.
export function HallsDevicesDestination({
  backend,
  session,
  zones,
  deviceInventory,
  onDeviceInventoryChange,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  errorDetail,
  onRetry
}: DestinationProps) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<HallsTab>('layout');

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const zoneRows = zones ?? [];
  const deviceRows = deviceInventory ?? [];

  const canManageLayout = hasPermission(session, permissionNames.manageLayout);
  const canAssignDeviceSeat = hasPermission(session, permissionNames.assignDeviceSeat);
  const canViewDeviceDetail = hasPermission(session, permissionNames.viewDeviceDetail);
  const canRotateDeviceCredential = hasPermission(session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = hasPermission(session, permissionNames.revokeDeviceCredential);

  const layoutSeatOptions = useMemo(() => zoneRows.flatMap((zone) =>
    readArray<Record<string, unknown>>(zone, 'seats').map((seat) => ({
      seatId: readString(seat, 'seatId'),
      label: `${readString(zone, 'name', t('op.settings.layout.zoneFallback'))} · ${readString(seat, 'name', t('op.settings.layout.seatFallback'))}`
    }))
  ).filter((seat) => isGuid(seat.seatId)), [zoneRows, t]);

  return (
    <ManagementScreen
      title={t('op.management.dest.halls')}
      subtitle={t('op.management.dest.halls.subtitle')}
      contentWidth="full"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="mgmt-tabs" role="tablist" aria-label={t('op.management.dest.halls')}>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'layout'}
          className={`mgmt-tab${activeTab === 'layout' ? ' is-active' : ''}`}
          onClick={() => setActiveTab('layout')}
        >
          {t('op.management.halls.tab.layout')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'devices'}
          className={`mgmt-tab${activeTab === 'devices' ? ' is-active' : ''}`}
          onClick={() => setActiveTab('devices')}
        >
          {t('op.management.halls.tab.devices')}
        </button>
      </div>

      {activeTab === 'layout' ? (
        <ZonesTab
          zones={zoneRows}
          backend={backend}
          canManageLayout={canManageLayout}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      ) : (
        <DevicesTab
          deviceInventory={deviceRows}
          layoutSeatOptions={layoutSeatOptions}
          backend={backend}
          canAssignDeviceSeat={canAssignDeviceSeat}
          canViewDeviceDetail={canViewDeviceDetail}
          canRotateDeviceCredential={canRotateDeviceCredential}
          canRevokeDeviceCredential={canRevokeDeviceCredential}
          onDeviceInventoryChange={onDeviceInventoryChange ?? (() => {})}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      )}
    </ManagementScreen>
  );
}
