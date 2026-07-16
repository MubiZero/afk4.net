import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { SettingsLayoutSection } from '../../settings/SettingsLayoutSection';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { managementScreenState, type DestinationProps } from './types';

// Залы и ПК: тонкая обёртка над SettingsLayoutSection (зоны/места + устройства/команды/ключи) в
// общем каркасе ManagementScreen. Раздел сохраняет по-действию (создать зону, назначить
// устройство и т.п.), поэтому без save-бара — dirty-guard тут не нужен, сигнализируем "clean"
// сразу на маунте.
export function HallsDevicesDestination({
  backend,
  session,
  zones,
  deviceInventory,
  branchDeviceCommandHistory,
  onDeviceInventoryChange,
  onBranchDeviceCommandHistoryChange,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  errorDetail,
  onRetry
}: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const canManageLayout = hasPermission(session, permissionNames.manageLayout);
  const canCreateDeviceEnrollmentCode = hasPermission(session, permissionNames.createDeviceEnrollmentCode);
  const canAssignDeviceSeat = hasPermission(session, permissionNames.assignDeviceSeat);
  const canViewDeviceDetail = hasPermission(session, permissionNames.viewDeviceDetail);
  const canViewDeviceCommandStatus = hasPermission(session, permissionNames.viewDeviceCommandStatus);
  const canDispatchDeviceCommand = hasPermission(session, permissionNames.dispatchDeviceCommand);
  const canRotateDeviceCredential = hasPermission(session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = hasPermission(session, permissionNames.revokeDeviceCredential);

  return (
    <ManagementScreen
      title={t('op.management.dest.halls')}
      subtitle={t('op.management.dest.halls.subtitle')}
      contentWidth="wide"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="management-panel">
        <SettingsLayoutSection
          zones={zones ?? []}
          deviceInventory={deviceInventory ?? []}
          branchDeviceCommandHistory={branchDeviceCommandHistory ?? []}
          backend={backend}
          canManageLayout={canManageLayout}
          canCreateDeviceEnrollmentCode={canCreateDeviceEnrollmentCode}
          canAssignDeviceSeat={canAssignDeviceSeat}
          canViewDeviceDetail={canViewDeviceDetail}
          canViewDeviceCommandStatus={canViewDeviceCommandStatus}
          canDispatchDeviceCommand={canDispatchDeviceCommand}
          canRotateDeviceCredential={canRotateDeviceCredential}
          canRevokeDeviceCredential={canRevokeDeviceCredential}
          onBranchDeviceCommandHistoryChange={onBranchDeviceCommandHistoryChange ?? (() => {})}
          onDeviceInventoryChange={onDeviceInventoryChange ?? (() => {})}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      </div>
    </ManagementScreen>
  );
}
