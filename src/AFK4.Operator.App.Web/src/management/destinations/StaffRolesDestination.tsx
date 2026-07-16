import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { SettingsStaffSection } from '../../settings/SettingsStaffSection';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { managementScreenState, type DestinationProps } from './types';

// Сотрудники и роли: тонкая обёртка над SettingsStaffSection (приглашение, профиль, роли,
// вкл/выкл, сброс пароля) в общем каркасе ManagementScreen. Раздел сохраняет по-действию,
// поэтому без save-бара — сигнализируем "clean" сразу на маунте.
export function StaffRolesDestination({
  backend,
  session,
  staffUsers,
  onStaffUsersChange,
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

  const canManageBranchStaff = backend !== null && hasPermission(session, permissionNames.manageBranchStaff);
  const canManageRoles = backend !== null && hasPermission(session, permissionNames.manageRoles);

  return (
    <ManagementScreen
      title={t('op.management.dest.staff')}
      subtitle={t('op.management.dest.staff.subtitle')}
      contentWidth="wide"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="management-panel">
        <SettingsStaffSection
          staffUsers={staffUsers ?? []}
          backend={backend}
          canManageBranchStaff={canManageBranchStaff}
          canManageRoles={canManageRoles}
          onStaffUsersChange={onStaffUsersChange ?? (() => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      </div>
    </ManagementScreen>
  );
}
