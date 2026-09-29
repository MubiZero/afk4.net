import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Tabs } from '@afk4/ui/react';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { isGuid } from '../../operatorHelpers';
import { projectOperatorError } from '../../apiErrors';
import { LoadFailureState } from '../../operatorPrimitives';
import { managementScreenState, type DestinationProps } from './types';
import { ZonesTab, ZonesTabSkeleton } from './halls/ZonesTab';
import { DevicesTab, DevicesTabSkeleton } from './halls/DevicesTab';
import { DeferredSkeleton } from '../../LoadingSkeleton';

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
  deviceState,
  onDeviceInventoryChange,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  failure,
  onRetry,
  onRetryDevices
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
  const canViewDeviceCommands = hasPermission(session, permissionNames.viewDeviceCommandStatus);
  const canRotateDeviceCredential = hasPermission(session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = hasPermission(session, permissionNames.revokeDeviceCredential);
  const canManageBranchSettings = hasPermission(session, permissionNames.manageBranchSettings);

  const layoutSeatOptions = useMemo(() => zoneRows.flatMap((zone) =>
    zone.seats.map((seat) => ({
      seatId: seat.seatId,
      label: `${zone.name || t('op.settings.layout.zoneFallback')} · ${seat.name || t('op.settings.layout.seatFallback')}`
    }))
  ).filter((seat) => isGuid(seat.seatId)), [zoneRows, t]);

  return (
    <ManagementScreen
      title={t('op.management.dest.halls')}
      contentWidth="full"
      // Вкладки — второй строкой шапки раздела: при загрузке они уже на месте и не прыгают.
      tabs={
        <Tabs
          label={t('op.management.dest.halls')}
          value={activeTab}
          onChange={setActiveTab}
          items={[
            { value: 'layout', label: t('op.management.halls.tab.layout') },
            { value: 'devices', label: t('op.management.halls.tab.devices') }
          ]}
        />
      }
      state={managementScreenState(loadStatus)}
      skeleton={activeTab === 'layout' ? <ZonesTabSkeleton canManageLayout={canManageLayout} /> : <DevicesTabSkeleton />}
      failure={failure}
      onRetry={onRetry}
    >
      {activeTab === 'layout' ? (
        <ZonesTab
          zones={zoneRows}
          backend={backend}
          canManageLayout={canManageLayout}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      ) : deviceState?.status === 'failed' ? (
        // Список устройств грузится отдельно от залов: его отказ называется здесь, а вкладка
        // «Залы и места» остаётся рабочей. Раньше отказ молча превращался в «устройств нет».
        <LoadFailureState
          title={t('op.management.halls.devices.loadFailed')}
          failure={deviceState.failure ?? projectOperatorError(undefined, t)}
          onRetry={() => onRetryDevices?.()}
        />
      ) : deviceState?.status === 'loading' && deviceState.data.length === 0 ? (
        <DeferredSkeleton><DevicesTabSkeleton /></DeferredSkeleton>
      ) : (
        <DevicesTab
          deviceInventory={deviceRows}
          layoutSeatOptions={layoutSeatOptions}
          backend={backend}
          canAssignDeviceSeat={canAssignDeviceSeat}
          canViewDeviceDetail={canViewDeviceDetail}
          canViewDeviceCommands={canViewDeviceCommands}
          canRotateDeviceCredential={canRotateDeviceCredential}
          canRevokeDeviceCredential={canRevokeDeviceCredential}
          canManageBranchSettings={canManageBranchSettings}
          onDeviceInventoryChange={onDeviceInventoryChange ?? (() => {})}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      )}
    </ManagementScreen>
  );
}
