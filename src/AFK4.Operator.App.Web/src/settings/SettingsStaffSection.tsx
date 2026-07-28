import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import type { StaffUserDto } from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames, staffRoleOptions } from '../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  isGuid,
  operatorDisplayNameLabel,
  readArray,
  readBoolean,
  readString,
  requireBackend,
  staffRoleLabel,
  triggerFeedback
} from '../operatorHelpers';

// Раздел «Персонал»: владеет своей формой приглашения, выбором пользователя и мутациями.
// Родитель отдаёт серверный список staffUsers + сеттер onStaffUsersChange + onFeedback.
export function SettingsStaffSection({
  staffUsers,
  backend,
  canManageBranchStaff,
  canManageRoles,
  onStaffUsersChange,
  onFeedback
}: {
  staffUsers: StaffUserDto[];
  backend: OperatorBackendContext | null;
  canManageBranchStaff: boolean;
  canManageRoles: boolean;
  onStaffUsersChange: (users: StaffUserDto[]) => void;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();

  // Локальные копии quick-action констант (дублирование намеренное — родитель держит свои для панели)
  const inviteStaffActionKey = t('op.settings.action.inviteStaff');
  const updateStaffProfileActionKey = t('op.settings.action.updateStaffProfile');
  const updateRoleActionKey = t('op.settings.action.updateRole');
  const disableStaffActionKey = t('op.settings.action.disableStaff');
  const enableStaffActionKey = t('op.settings.action.enableStaff');
  const resetPasswordActionKey = t('op.settings.action.resetPassword');

  const [selectedStaffUserId, setSelectedStaffUserId] = useState('');
  const [staffProfileUserName, setStaffProfileUserName] = useState('');
  const [staffProfileDisplayName, setStaffProfileDisplayName] = useState('');
  const [staffRoleName, setStaffRoleName] = useState('operator');
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState(() => t('op.settings.prefill.inviteDisplayName'));
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [inviteRoleName, setInviteRoleName] = useState('operator');
  const [resetPassword, setResetPassword] = useState('');

  // Засев выбора из загруженных данных: сохранить текущего, если он ещё есть; иначе первый
  useEffect(() => {
    const staffRows = staffUsers;
    const selectedStaff = staffRows.find((user) => readString(user, 'staffUserId') === selectedStaffUserId) ?? staffRows[0];
    const selectedStaffRole = readArray<string>(selectedStaff, 'roleNames')
      .find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
    setSelectedStaffUserId(readString(selectedStaff, 'staffUserId'));
    setStaffProfileUserName(readString(selectedStaff, 'userName'));
    setStaffProfileDisplayName(operatorDisplayNameLabel(readString(selectedStaff, 'displayName'), t));
    setStaffRoleName(selectedStaffRole ?? 'operator');
  }, [staffUsers]);

  const selectedStaffUser = staffUsers.find((user) => readString(user, 'staffUserId') === selectedStaffUserId);
  const selectedStaffIsActive = readBoolean(selectedStaffUser, 'isActive', true);

  const runAction = async (label: string) => {
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === inviteStaffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const userName = inviteUserName.trim();
        const displayName = inviteDisplayName.trim();
        const email = inviteEmail.trim();
        if (!userName || !displayName || !email) {
          throw new Error(t('op.settings.staff.error.fillInvite'));
        }

        const invite = await apiClients.settings.createStaffInvite(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName,
          email,
          roleNames: [inviteRoleName || 'operator']
        });
        // The invitee appears in the staff list only after they accept and set a password.
        setInviteCode(invite.code);
        setInviteUserName(`operator${staffUsers.length + 2}`);
        setInviteDisplayName(t('op.settings.prefill.inviteDisplayName')); // editable prefill
        setInviteEmail('');
      } else if (label === updateStaffProfileActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const userName = staffProfileUserName.trim();
        const displayName = staffProfileDisplayName.trim();
        if (!isGuid(staffUserId) || !userName || !displayName) {
          throw new Error(t('op.settings.staff.error.fillProfile'));
        }

        const staffUser = await apiClients.settings.updateStaffUserProfile(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName
        });
        onStaffUsersChange(staffUsers.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffProfileUserName(readString(staffUser, 'userName'));
        setStaffProfileDisplayName(operatorDisplayNameLabel(readString(staffUser, 'displayName'), t));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === updateRoleActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageRoles)) {
          throw new Error(t('op.settings.staff.error.noPermRoles'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const roleName = staffRoleName.trim();
        if (!isGuid(staffUserId) || !staffRoleOptions.includes(roleName as (typeof staffRoleOptions)[number])) {
          throw new Error(t('op.settings.staff.error.selectStaffAndRole'));
        }

        const staffUser = await apiClients.settings.updateStaffUserRoles(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          roleNames: [roleName]
        });
        onStaffUsersChange(staffUsers.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? roleName);
      } else if (label === disableStaffActionKey || label === enableStaffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        if (!isGuid(staffUserId)) {
          throw new Error(t('op.settings.staff.error.selectStaff'));
        }

        const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          isActive: label === enableStaffActionKey
        });
        onStaffUsersChange(staffUsers.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === resetPasswordActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const nextPassword = resetPassword.trim();
        if (!isGuid(staffUserId) || nextPassword.length < 8) {
          throw new Error(t('op.settings.staff.error.passwordTooShort'));
        }

        const staffUser = await apiClients.settings.resetStaffUserPassword(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          newPassword: nextPassword
        });
        onStaffUsersChange(staffUsers.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setResetPassword('');
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.staff.title')}</span>
        <div className="settings-section-actions">
          <button type="button" disabled={!canManageBranchStaff} onClick={() => runAction(inviteStaffActionKey)}>{inviteStaffActionKey}</button>
          <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runAction(selectedStaffIsActive ? disableStaffActionKey : enableStaffActionKey)}>
            {selectedStaffIsActive ? disableStaffActionKey : enableStaffActionKey}
          </button>
          <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runAction(resetPasswordActionKey)}>{resetPasswordActionKey}</button>
        </div>
      </div>
      <div className="settings-staff-layout">
        <div className="settings-config-grid">
          {staffUsers.map((user) => (
            <button
              key={readString(user, 'staffUserId')}
              type="button"
              className={readString(user, 'staffUserId') === selectedStaffUserId ? 'active' : ''}
              onClick={() => {
                const roleName = readArray<string>(user, 'roleNames').find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
                setSelectedStaffUserId(readString(user, 'staffUserId'));
                setStaffProfileUserName(readString(user, 'userName'));
                setStaffProfileDisplayName(operatorDisplayNameLabel(readString(user, 'displayName'), t));
                setStaffRoleName(roleName ?? 'operator');
                triggerFeedback(onFeedback, operatorDisplayNameLabel(readString(user, 'displayName'), t), 'confirmed');
              }}
            >
              <strong>{operatorDisplayNameLabel(readString(user, 'displayName'), t)}</strong>
              <span>{readString(user, 'userName', t('op.settings.staff.userFallback'))} · {readArray<string>(user, 'roleNames').map((r) => staffRoleLabel(r, t)).join(', ') || t('op.settings.staff.roleEmpty')} · {readBoolean(user, 'isActive', true) ? t('op.settings.staff.active') : t('op.settings.staff.inactive')}</span>
            </button>
          ))}
        </div>
        <div className="settings-form-grid settings-staff-form">
          <label>{t('op.settings.staff.loginLabel')}<input value={inviteUserName} disabled={!canManageBranchStaff} onChange={(event) => setInviteUserName(event.currentTarget.value)} /></label>
          <label>{t('op.settings.staff.displayName')}<input value={inviteDisplayName} disabled={!canManageBranchStaff} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} /></label>
          <label>{t('op.settings.staff.email')}<input type="email" value={inviteEmail} disabled={!canManageBranchStaff} onChange={(event) => setInviteEmail(event.currentTarget.value)} /></label>
          {inviteCode && <label>{t('op.settings.staff.inviteCode')}<input readOnly value={inviteCode} onFocus={(event) => event.currentTarget.select()} /></label>}
          <label>{t('op.settings.staff.roleAccess')}
            <select value={inviteRoleName} disabled={!canManageBranchStaff} onChange={(event) => setInviteRoleName(event.currentTarget.value)}>
              {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName, t)}</option>)}
            </select>
          </label>
          <label>{t('op.settings.staff.profileLogin')}<input value={staffProfileUserName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileUserName(event.currentTarget.value)} /></label>
          <label>{t('op.settings.staff.profileName')}<input value={staffProfileDisplayName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileDisplayName(event.currentTarget.value)} /></label>
          <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runAction(updateStaffProfileActionKey)}>{t('op.settings.staff.updateProfileBtn')}</button>
          <label>{t('op.settings.staff.newRole')}
            <select value={staffRoleName} disabled={!canManageRoles || !selectedStaffUserId} onChange={(event) => setStaffRoleName(event.currentTarget.value)}>
              {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName, t)}</option>)}
            </select>
          </label>
          <label>{t('op.settings.staff.newPassword')}<input type="password" value={resetPassword} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setResetPassword(event.currentTarget.value)} /></label>
          <button type="button" disabled={!canManageRoles || !selectedStaffUserId} onClick={() => runAction(updateRoleActionKey)}>{updateRoleActionKey}</button>
        </div>
      </div>
    </>
  );
}
