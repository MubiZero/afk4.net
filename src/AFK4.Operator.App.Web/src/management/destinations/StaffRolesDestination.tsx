import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { KeyRound, Pencil, Power, PowerOff, Users } from 'lucide-react';
import { ManagementScreen } from '../ManagementScreen';
import { MgmtTable } from '../kit/MgmtTable';
import { MgmtDrawer } from '../kit/MgmtDrawer';
import type { RowAction } from '../kit/types';
import { PanelModal } from '../../PanelModal';
import { CriticalActionConfirmation } from '../../operatorPrimitives';
import { projectOperatorError } from '../../apiErrors';
import { hasPermission, permissionNames, staffRoleOptions } from '../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  isGuid,
  operatorDisplayNameLabel,
  readArray,
  readBoolean,
  readString,
  requireBackend,
  staffRoleLabel
} from '../../operatorHelpers';
import { managementScreenState, type DestinationProps } from './types';

type StaffUser = Record<string, unknown>;

export function toggleRole(current: string[], role: string): string[] {
  const next = current.includes(role)
    ? current.filter((candidate) => candidate !== role)
    : [...current, role];
  return staffRoleOptions.filter((candidate) => next.includes(candidate));
}

type CriticalAction =
  | { kind: 'disable'; staffUserId: string; name: string }
  | { kind: 'reset-password'; staffUserId: string; name: string; newPassword: string };

// Сотрудники и роли: список+drawer CRUD по эталону «Залы и ПК»/«Тарифы» (см.
// task-C2-staff-brief.md). Одна сущность — сотрудники (набор ролей — в карточке, не отдельный
// список). Все 5 операций (пригласить/профиль/роль/вкл-выкл/сброс пароля) портированы 1:1 из
// settings/SettingsStaffSection.tsx.runAction — те же клиентские вызовы, тот же двойной
// permission-гейт (can*-проп + серверный hasPermission на каждый вызов), тот же мёрж результата
// в staffUsers через onStaffUsersChange (без full reload — сервер не отдаёт список целиком).
// Усиление против оригинала: отключение доступа и сброс пароля идут через
// CriticalActionConfirmation (в старой форме их можно было нажать одним кликом).
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
  const staffRows = staffUsers ?? [];

  const [selectedStaffUserId, setSelectedStaffUserId] = useState<string | null>(null);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [criticalAction, setCriticalAction] = useState<CriticalAction | null>(null);
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState(() => t('op.settings.prefill.inviteDisplayName'));
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRoleNames, setInviteRoleNames] = useState<string[]>(['operator']);
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [profileUserName, setProfileUserName] = useState('');
  const [profileDisplayName, setProfileDisplayName] = useState('');
  const [roleNames, setRoleNames] = useState<string[]>(['operator']);
  const [newPassword, setNewPassword] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  // Если выбранный сотрудник пропал из выборки (мёрж после reload) — закрыть drawer, а не
  // показывать устаревшую запись.
  useEffect(() => {
    setSelectedStaffUserId((current) => (current && staffRows.some((user) => readString(user, 'staffUserId') === current) ? current : null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [staffRows]);

  const selectedStaffUser = staffRows.find((user) => readString(user, 'staffUserId') === selectedStaffUserId) ?? null;
  const selectedStaffIsActive = readBoolean(selectedStaffUser, 'isActive', true);
  const selectedStaffIsSelf = selectedStaffUserId !== null
    && session?.staffUserId?.toLowerCase() === selectedStaffUserId.toLowerCase();

  // Засев формы drawer'а из выбранного сотрудника.
  useEffect(() => {
    if (!selectedStaffUser) return;
    setProfileUserName(readString(selectedStaffUser, 'userName'));
    setProfileDisplayName(operatorDisplayNameLabel(readString(selectedStaffUser, 'displayName'), t));
    const currentRoles = readArray<string>(selectedStaffUser, 'roleNames')
      .filter((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
    setRoleNames(currentRoles);
    setNewPassword('');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedStaffUserId]);

  const canManageBranchStaff = backend !== null && hasPermission(session, permissionNames.manageBranchStaff);
  const canManageRoles = backend !== null && hasPermission(session, permissionNames.manageRoles);

  const mergeStaffUser = (staffUser: StaffUser) => {
    const staffUserId = readString(staffUser, 'staffUserId');
    onStaffUsersChange?.(staffRows.map((item) => (readString(item, 'staffUserId') === staffUserId ? staffUser : item)));
  };

  const openInvite = () => {
    setInviteUserName(`operator${staffRows.length + 1}`);
    setInviteDisplayName(t('op.settings.prefill.inviteDisplayName'));
    setInviteEmail('');
    setInviteRoleNames(['operator']);
    setInviteCode(null);
    setInviteOpen(true);
  };

  const submitInvite = async () => {
    const label = t('op.settings.action.inviteStaff');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
        throw new Error(t('op.settings.staff.error.noPerm'));
      }

      const userName = inviteUserName.trim();
      const displayName = inviteDisplayName.trim();
      const email = inviteEmail.trim();
      if (!userName || !displayName || !email || inviteRoleNames.length === 0) {
        throw new Error(t('op.settings.staff.error.fillInvite'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const invite = await apiClients.settings.createStaffInvite(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        userName,
        displayName,
        email,
        roleNames: inviteRoleNames
      });
      // Приглашённый появится в списке сотрудников только после того как примет приглашение и
      // задаст пароль — поэтому список не обновляем, только показываем код.
      setInviteCode(invite.code);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitProfile = async () => {
    if (!selectedStaffUser) return;
    const label = t('op.settings.action.updateStaffProfile');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
        throw new Error(t('op.settings.staff.error.noPerm'));
      }

      const staffUserId = readString(selectedStaffUser, 'staffUserId');
      const userName = profileUserName.trim();
      const displayName = profileDisplayName.trim();
      if (!isGuid(staffUserId) || !userName || !displayName) {
        throw new Error(t('op.settings.staff.error.fillProfile'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const staffUser = await apiClients.settings.updateStaffUserProfile(nextBackend.branchId, staffUserId, {
        organizationId: nextBackend.session.organizationId,
        userName,
        displayName
      });
      mergeStaffUser(staffUser);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitRole = async () => {
    if (!selectedStaffUser) return;
    const label = t('op.settings.action.updateRole');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageRoles)) {
        throw new Error(t('op.settings.staff.error.noPermRoles'));
      }

      const staffUserId = readString(selectedStaffUser, 'staffUserId');
      if (!isGuid(staffUserId) || roleNames.length === 0 || roleNames.some((role) => !staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]))) {
        throw new Error(t('op.settings.staff.error.selectStaffAndRole'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const staffUser = await apiClients.settings.updateStaffUserRoles(nextBackend.branchId, staffUserId, {
        organizationId: nextBackend.session.organizationId,
        roleNames
      });
      mergeStaffUser(staffUser);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitEnable = async (staffUserId: string) => {
    const label = t('op.settings.action.enableStaff');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
        throw new Error(t('op.settings.staff.error.noPerm'));
      }
      if (!isGuid(staffUserId)) {
        throw new Error(t('op.settings.staff.error.selectStaff'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, staffUserId, {
        organizationId: nextBackend.session.organizationId,
        isActive: true
      });
      mergeStaffUser(staffUser);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  // Включение доступа безобидно (обратимо в один клик) — идёт сразу. Отключение — через
  // CriticalActionConfirmation, т.к. блокирует вход сотруднику (усиление против оригинала).
  const toggleAccess = (staffUser: StaffUser) => {
    const staffUserId = readString(staffUser, 'staffUserId');
    const name = operatorDisplayNameLabel(readString(staffUser, 'displayName'), t);
    if (readBoolean(staffUser, 'isActive', true)) {
      setCriticalAction({ kind: 'disable', staffUserId, name });
    } else {
      void submitEnable(staffUserId);
    }
  };

  // Валидация длины — сразу, без диалога подтверждения (тот же текст ошибки, что и в
  // оригинале); подтверждение открывается только для валидного пароля.
  const requestResetPassword = () => {
    if (!selectedStaffUser) return;
    const staffUserId = readString(selectedStaffUser, 'staffUserId');
    const trimmedPassword = newPassword.trim();
    if (!isGuid(staffUserId) || trimmedPassword.length < 8) {
      onFeedback?.({ label: t('op.settings.action.resetPassword'), state: 'failed', detail: t('op.settings.staff.error.passwordTooShort') });
      return;
    }

    setCriticalAction({
      kind: 'reset-password',
      staffUserId,
      name: operatorDisplayNameLabel(readString(selectedStaffUser, 'displayName'), t),
      newPassword: trimmedPassword
    });
  };

  const confirmCriticalAction = async () => {
    if (!criticalAction) return;
    const action = criticalAction;
    setCriticalAction(null);

    if (action.kind === 'disable') {
      const label = t('op.settings.action.disableStaff');
      onFeedback?.({ label, state: 'pending' });
      try {
        const nextBackend = requireBackend(backend, t);
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
        const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, action.staffUserId, {
          organizationId: nextBackend.session.organizationId,
          isActive: false
        });
        mergeStaffUser(staffUser);
        onFeedback?.({ label, state: 'confirmed' });
      } catch (error) {
        onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
      }
    } else {
      const label = t('op.settings.action.resetPassword');
      onFeedback?.({ label, state: 'pending' });
      try {
        const nextBackend = requireBackend(backend, t);
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
        const staffUser = await apiClients.settings.resetStaffUserPassword(nextBackend.branchId, action.staffUserId, {
          organizationId: nextBackend.session.organizationId,
          newPassword: action.newPassword
        });
        mergeStaffUser(staffUser);
        setNewPassword('');
        onFeedback?.({ label, state: 'confirmed' });
      } catch (error) {
        onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
      }
    }
  };

  const rowActions = canManageBranchStaff
    ? (staffUser: StaffUser): RowAction[] => {
      const isActive = readBoolean(staffUser, 'isActive', true);
      const isSelf = readString(staffUser, 'staffUserId').toLowerCase() === session?.staffUserId?.toLowerCase();
      return [
        {
          id: 'edit',
          label: t('op.management.staff.editCta'),
          icon: <Pencil size={14} aria-hidden="true" />,
          onSelect: () => setSelectedStaffUserId(readString(staffUser, 'staffUserId'))
        },
        ...(!isSelf ? [{
          id: 'toggle-access',
          label: isActive ? t('op.settings.action.disableStaff') : t('op.settings.action.enableStaff'),
          icon: isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />,
          danger: isActive,
          onSelect: () => toggleAccess(staffUser)
        } satisfies RowAction] : []),
        {
          id: 'reset-password',
          label: t('op.settings.action.resetPassword'),
          icon: <KeyRound size={14} aria-hidden="true" />,
          onSelect: () => setSelectedStaffUserId(readString(staffUser, 'staffUserId'))
        }
      ];
    }
    : undefined;

  return (
    <ManagementScreen
      title={t('op.management.dest.staff')}
      subtitle={t('op.management.dest.staff.subtitle')}
      contentWidth="full"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="mgmt-master-detail">
        <MgmtTable<StaffUser>
          columns={[
            { key: 'name', header: t('op.management.staff.col.name'), render: (staffUser) => operatorDisplayNameLabel(readString(staffUser, 'displayName'), t) },
            { key: 'login', header: t('op.management.staff.col.login'), render: (staffUser) => readString(staffUser, 'userName', t('op.settings.staff.userFallback')) },
            {
              key: 'role',
              header: t('op.management.staff.col.role'),
              render: (staffUser) => readArray<string>(staffUser, 'roleNames').map((role) => staffRoleLabel(role, t)).join(', ') || t('op.settings.staff.roleEmpty')
            },
            {
              key: 'status',
              header: t('op.management.staff.col.status'),
              render: (staffUser) => readBoolean(staffUser, 'isActive', true) ? t('op.settings.staff.active') : t('op.settings.staff.inactive')
            }
          ]}
          rows={staffRows}
          rowKey={(staffUser) => readString(staffUser, 'staffUserId')}
          gridTemplate="1.3fr 1fr 1.1fr 0.8fr"
          selectedKey={selectedStaffUserId}
          onSelectRow={(staffUser) => setSelectedStaffUserId(readString(staffUser, 'staffUserId'))}
          rowActions={rowActions}
          toolbar={{
            title: t('op.settings.staff.title'),
            primary: canManageBranchStaff ? { label: t('op.management.staff.addStaffCta'), onClick: openInvite } : undefined
          }}
          empty={{
            icon: <Users size={22} aria-hidden="true" />,
            title: t('op.management.staff.staffEmpty.title'),
            description: t('op.management.staff.staffEmpty.description'),
            action: canManageBranchStaff ? { label: t('op.management.staff.addStaffCta'), onClick: openInvite } : undefined
          }}
        />

        {selectedStaffUser && (
          <MgmtDrawer
            title={operatorDisplayNameLabel(readString(selectedStaffUser, 'displayName'), t)}
            subtitle={selectedStaffIsActive ? t('op.settings.staff.active') : t('op.settings.staff.inactive')}
            onClose={() => setSelectedStaffUserId(null)}
          >
            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.management.staff.profileSection.title')}</span></div>
              <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitProfile(); }}>
                <div className="mgmt-form-grid">
                  <label>{t('op.settings.staff.profileLogin')}
                    <input value={profileUserName} disabled={!canManageBranchStaff || busy} onChange={(event) => setProfileUserName(event.currentTarget.value)} />
                  </label>
                  <label>{t('op.settings.staff.profileName')}
                    <input value={profileDisplayName} disabled={!canManageBranchStaff || busy} onChange={(event) => setProfileDisplayName(event.currentTarget.value)} />
                  </label>
                </div>
                <div className="mgmt-form-actions">
                  <button type="submit" className="ui-btn ui-btn--primary" disabled={!canManageBranchStaff || busy}>{t('common.save')}</button>
                </div>
              </form>
            </div>

            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.management.staff.roleSection.title')}</span></div>
              <div className="mgmt-form">
                <div className="mgmt-form-grid">
                  <fieldset className="mgmt-role-set">
                    <legend>{t('op.management.staff.roleSection.rolesLabel')}</legend>
                    {staffRoleOptions.map((role) => (
                      <label key={role} className="mgmt-check">
                        <input
                          type="checkbox"
                          checked={roleNames.includes(role)}
                          disabled={!canManageRoles || busy}
                          onChange={() => setRoleNames((current) => toggleRole(current, role))}
                        />
                        {staffRoleLabel(role, t)}
                      </label>
                    ))}
                  </fieldset>
                </div>
                <div className="mgmt-form-actions">
                  <button type="button" className="ui-btn ui-btn--primary" disabled={!canManageRoles || busy || roleNames.length === 0} onClick={() => void submitRole()}>
                    {t('op.settings.action.updateRole')}
                  </button>
                </div>
              </div>
            </div>

            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.management.staff.accessSection.title')}</span></div>
              <div className="mgmt-form">
                <div className="mgmt-form-actions">
                  {!selectedStaffIsSelf && (
                    <button
                      type="button"
                      className={selectedStaffIsActive ? 'ui-btn ui-btn--danger' : 'ui-btn'}
                      disabled={!canManageBranchStaff || busy}
                      onClick={() => toggleAccess(selectedStaffUser)}
                    >
                      {selectedStaffIsActive ? t('op.settings.action.disableStaff') : t('op.settings.action.enableStaff')}
                    </button>
                  )}
                </div>
                <div className="mgmt-form-grid">
                  <label className="mgmt-form-wide">{t('op.settings.staff.newPassword')}
                    <input
                      type="password"
                      value={newPassword}
                      disabled={!canManageBranchStaff || busy}
                      onChange={(event) => setNewPassword(event.currentTarget.value)}
                    />
                  </label>
                </div>
                <div className="mgmt-form-actions">
                  <button type="button" className="ui-btn" disabled={!canManageBranchStaff || busy} onClick={requestResetPassword}>
                    {t('op.settings.action.resetPassword')}
                  </button>
                </div>
              </div>
            </div>
          </MgmtDrawer>
        )}
      </div>

      {inviteOpen && (
        <PanelModal title={t('op.management.staff.inviteModal.title')} onClose={() => setInviteOpen(false)} closeDisabled={busy}>
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitInvite(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.staff.loginLabel')}
                <input value={inviteUserName} disabled={busy} onChange={(event) => setInviteUserName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.staff.displayName')}
                <input value={inviteDisplayName} disabled={busy} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.staff.email')}
                <input type="email" value={inviteEmail} disabled={busy} onChange={(event) => setInviteEmail(event.currentTarget.value)} />
              </label>
              <fieldset className="mgmt-role-set">
                <legend>{t('op.management.staff.inviteModal.rolesLabel')}</legend>
                {staffRoleOptions.map((role) => (
                  <label key={role} className="mgmt-check">
                    <input
                      type="checkbox"
                      checked={inviteRoleNames.includes(role)}
                      disabled={busy}
                      onChange={() => setInviteRoleNames((current) => toggleRole(current, role))}
                    />
                    {staffRoleLabel(role, t)}
                  </label>
                ))}
              </fieldset>
            </div>

            {inviteCode && (
              <>
                <div className="mgmt-form-grid">
                  <label className="mgmt-form-wide">{t('op.settings.staff.inviteCode')}
                    <input readOnly value={inviteCode} onFocus={(event) => event.currentTarget.select()} />
                  </label>
                </div>
                <p className="mgmt-drawer-hint">{t('op.management.staff.inviteModal.codeHint')}</p>
              </>
            )}

            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setInviteOpen(false)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || inviteRoleNames.length === 0}>{t('op.settings.action.inviteStaff')}</button>
            </div>
          </form>
        </PanelModal>
      )}

      {criticalAction?.kind === 'disable' && (
        <CriticalActionConfirmation
          title={t('op.management.staff.confirmDisable.title')}
          detail={criticalAction.name}
          impact={t('op.management.staff.confirmDisable.impact')}
          confirmLabel={t('op.settings.action.disableStaff')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmCriticalAction()}
        />
      )}
      {criticalAction?.kind === 'reset-password' && (
        <CriticalActionConfirmation
          title={t('op.management.staff.confirmResetPassword.title')}
          detail={criticalAction.name}
          impact={t('op.management.staff.confirmResetPassword.impact')}
          confirmLabel={t('op.settings.action.resetPassword')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmCriticalAction()}
        />
      )}
    </ManagementScreen>
  );
}
