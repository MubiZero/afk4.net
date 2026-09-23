import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonTable } from '@/components/ui/skeletons';
import { Dialog } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useBlockedReason } from '@/components/ui/blockedReason';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdminsApi } from '@/api/platformClients/admins';
import type { TwoFactorApi } from '@/api/platformClients/twoFactor';
import type { RolesApi } from '@/api/platformClients/roles';
import { RolesSection } from './RolesSection';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import type { PlatformAdminInvitation, PlatformAdminListItem } from '@/api/types';
import { useAdmins } from './useAdmins';
import { AdminInviteDialog } from './AdminInviteDialog';
import {
  ROLE_PLATFORM_ADMIN,
  changeRoleBlockReasonKey,
  describeAdminActionError,
  disableBlockReasonKey,
  roleLabelKey
} from './adminsModel';

type TwoFactorResetClient = Pick<TwoFactorApi, 'reset'>;
type RolesClient = Pick<RolesApi, 'listRoles' | 'listPermissions' | 'createRole' | 'updateRole' | 'deleteRole'>;

export function SettingsScreen({ client, twoFactorClient, rolesClient, session }: {
  client: AdminsApi;
  twoFactorClient: TwoFactorResetClient;
  rolesClient: RolesClient;
  session: PlatformAdminSession;
}) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const { admins: adminsState, invitations: invitationsState } = useAdmins(client);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<PlatformAdminInvitation | null>(null);
  const [resetTarget, setResetTarget] = useState<PlatformAdminListItem | null>(null);
  // Отключить коллеге доступ к платформе или поменять ему роль — одного клика мало: соседние
  // действия того же веса (отзыв приглашения, сброс второго фактора, приостановка клуба) давно
  // спрашивают подтверждение, а эти два срабатывали мимо воли.
  const [confirmTarget, setConfirmTarget] = useState<{ kind: 'role' | 'active'; admin: PlatformAdminListItem } | null>(null);
  const [resetting, setResetting] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Действие над сотрудником меняет только список сотрудников, над приглашением — только
  // приглашения: перечитывать соседний список незачем.
  function refreshAdmins() {
    if (adminsState.status === 'ready') adminsState.retry();
  }

  function refreshInvitations() {
    if (invitationsState.status === 'ready') invitationsState.retry();
  }

  async function toggleActive(item: PlatformAdminListItem) {
    setBusyId(item.platformAdminUserId);
    try {
      await client.updateAdmin(item.platformAdminUserId, { isActive: !item.isActive });
      toast({ title: item.isActive ? t('platform.settings.disabled') : t('platform.settings.enabled'), variant: 'success' });
      refreshAdmins();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setBusyId(null);
    }
  }

  async function toggleRole(item: PlatformAdminListItem) {
    const nextRole = item.role === ROLE_PLATFORM_ADMIN ? 'platform_support' : ROLE_PLATFORM_ADMIN;
    setBusyId(item.platformAdminUserId);
    try {
      await client.updateAdmin(item.platformAdminUserId, { role: nextRole });
      toast({ title: t('platform.settings.roleChanged'), variant: 'success' });
      refreshAdmins();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setBusyId(null);
    }
  }

  async function resetTwoFactor() {
    if (resetTarget === null) return;
    setResetting(true);
    try {
      await twoFactorClient.reset(resetTarget.platformAdminUserId);
      toast({ title: t('platform.settings.resetTwoFactor.done'), variant: 'success' });
      setResetTarget(null);
      refreshAdmins();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setResetting(false);
    }
  }

  async function revoke(invitation: PlatformAdminInvitation) {
    setBusyId(invitation.invitationId);
    try {
      await client.revokeInvitation(invitation.invitationId);
      toast({ title: t('platform.settings.invite.revoked'), variant: 'success' });
      setRevokeTarget(null);
      refreshInvitations();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setBusyId(null);
    }
  }

  if (adminsState.status === 'loading') return <Loading><SkeletonCard action><SkeletonTable columns={6} /></SkeletonCard></Loading>;

  const admins = adminsState.status === 'ready' ? adminsState.data : [];
  const pendingInvitations = invitationsState.status === 'ready'
    ? invitationsState.data.filter(invitation => invitation.status === 'pending')
    : [];
  // «Сотрудников пока нет» — только когда оба списка пришли пустыми. Про список, который не
  // пришёл, пустотой не врём: у него своя причина рядом.
  const isEmpty = adminsState.status === 'ready' && invitationsState.status === 'ready'
    && admins.length === 0 && pendingInvitations.length === 0;
  const hasRows = admins.length > 0 || pendingInvitations.length > 0;

  return (
    <>
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.settings.title')}</CardTitle>
        <Button onClick={() => setInviteOpen(true)}>{t('platform.settings.action.invite')}</Button>
      </CardHeader>
      <CardContent>
        {adminsState.status === 'error' ? (
          <ErrorState
            title={t('platform.settings.admins.error.load')}
            message={adminsState.message}
            retryLabel={adminsState.canRetry ? t('state.retry') : undefined}
            onRetry={adminsState.canRetry ? adminsState.retry : undefined}
          />
        ) : null}
        {isEmpty ? (
          <EmptyState message={t('platform.settings.empty')} next={{ label: t('platform.settings.inviteFirst'), onClick: () => setInviteOpen(true) }} />
        ) : hasRows ? (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.settings.column.staff')}</TableHead>
                <TableHead>{t('platform.settings.column.role')}</TableHead>
                <TableHead>{t('platform.settings.column.twoFactor')}</TableHead>
                <TableHead>{t('platform.settings.column.lastSignIn')}</TableHead>
                <TableHead>{t('platform.settings.column.status')}</TableHead>
                <TableHead>{t('platform.settings.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {admins.map(item => {
                const disableReason = disableBlockReasonKey(item, session.platformAdminId, admins);
                const roleReason = changeRoleBlockReasonKey(item, session.platformAdminId, admins);
                const busy = busyId === item.platformAdminUserId;
                return (
                  <TableRow key={item.platformAdminUserId}>
                    <TableCell>
                      <span className="font-medium">{item.displayName}</span>{' '}
                      <code className="mgmt-drawer-hint">{item.userName}</code>
                    </TableCell>
                    <TableCell><Badge variant="outline">{t(roleLabelKey(item.role))}</Badge></TableCell>
                    <TableCell>
                      {item.twoFactorEnabled
                        ? <Badge variant="success">{t('platform.settings.twoFactor.on')}</Badge>
                        : <Badge variant="outline">{t('platform.settings.twoFactor.off')}</Badge>}
                    </TableCell>
                    <TableCell>{item.lastSignInAtUtc === null ? t('platform.settings.lastSignIn.never') : formatDate(item.lastSignInAtUtc)}</TableCell>
                    <TableCell>
                      {item.isActive
                        ? <Badge variant="success">{t('platform.settings.status.active')}</Badge>
                        : <Badge variant="outline">{t('platform.settings.status.inactive')}</Badge>}
                    </TableCell>
                    <TableCell>
                      <AdminActions
                        item={item}
                        busy={busy}
                        roleReason={roleReason === null ? null : t(roleReason)}
                        disableReason={disableReason === null ? null : t(disableReason)}
                        onChangeRole={() => setConfirmTarget({ kind: 'role', admin: item })}
                        onToggleActive={() => setConfirmTarget({ kind: 'active', admin: item })}
                        onResetTwoFactor={() => setResetTarget(item)}
                      />
                    </TableCell>
                  </TableRow>
                );
              })}
              {pendingInvitations.map(invitation => (
                <TableRow key={invitation.invitationId}>
                  <TableCell>{t('platform.settings.invite.rowLabel')}</TableCell>
                  <TableCell><Badge variant="outline">{t(roleLabelKey(invitation.role))}</Badge></TableCell>
                  <TableCell>—</TableCell>
                  <TableCell>{t('platform.settings.invite.expiresColumn', { date: formatDate(invitation.expiresAtUtc) })}</TableCell>
                  <TableCell><Badge variant="warning">{t('platform.settings.invite.status.pending')}</Badge></TableCell>
                  <TableCell>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={busyId === invitation.invitationId}
                      onClick={() => setRevokeTarget(invitation)}
                    >
                      {t('platform.settings.action.revoke')}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : null}
        {invitationsState.status === 'error' ? (
          <ErrorState
            title={t('platform.settings.invitations.error.load')}
            message={invitationsState.message}
            retryLabel={invitationsState.canRetry ? t('state.retry') : undefined}
            onRetry={invitationsState.canRetry ? invitationsState.retry : undefined}
          />
        ) : null}
      </CardContent>

      <AdminInviteDialog open={inviteOpen} client={client} onOpenChange={setInviteOpen} onCreated={refreshInvitations} />

      <ConfirmDialog
        open={confirmTarget !== null}
        title={confirmTarget?.kind === 'active'
          ? (confirmTarget.admin.isActive
            ? t('platform.settings.disableConfirm.title')
            : t('platform.settings.enableConfirm.title'))
          : t('platform.settings.roleConfirm.title')}
        description={confirmTarget?.kind === 'active'
          ? (confirmTarget.admin.isActive
            ? t('platform.settings.disableConfirm.body', { name: confirmTarget.admin.displayName })
            : t('platform.settings.enableConfirm.body', { name: confirmTarget.admin.displayName }))
          : t('platform.settings.roleConfirm.body', {
            name: confirmTarget?.admin.displayName ?? '',
            role: t(roleLabelKey(confirmTarget?.admin.role === ROLE_PLATFORM_ADMIN ? 'platform_support' : ROLE_PLATFORM_ADMIN))
          })}
        confirmLabel={t('common.confirm')}
        cancelLabel={t('common.cancel')}
        destructive={confirmTarget?.kind === 'active' && confirmTarget.admin.isActive}
        pending={busyId !== null}
        onConfirm={() => {
          const target = confirmTarget;
          setConfirmTarget(null);
          if (target === null) return;
          void (target.kind === 'active' ? toggleActive(target.admin) : toggleRole(target.admin));
        }}
        onOpenChange={open => { if (!open) setConfirmTarget(null); }}
      />

      <ConfirmDialog
        open={resetTarget !== null}
        title={t('platform.settings.resetTwoFactor.title')}
        description={t('platform.settings.resetTwoFactor.body')}
        confirmLabel={t('platform.settings.resetTwoFactor.confirm')}
        cancelLabel={t('common.cancel')}
        destructive
        pending={resetting}
        onConfirm={() => void resetTwoFactor()}
        onOpenChange={open => { if (!open) setResetTarget(null); }}
      />

      <Dialog
        open={revokeTarget !== null}
        title={t('platform.settings.invite.revokeConfirm.title')}
        description={t('platform.settings.invite.revokeConfirm.body')}
        tone="danger"
        onClose={() => setRevokeTarget(null)}
        footer={
          <>
            <Button variant="outline" onClick={() => setRevokeTarget(null)}>{t('common.cancel')}</Button>
            <Button
              variant="destructive"
              disabled={revokeTarget !== null && busyId === revokeTarget.invitationId}
              onClick={() => { if (revokeTarget !== null) void revoke(revokeTarget); }}
            >
              {t('platform.settings.invite.revokeConfirm.confirm')}
            </Button>
          </>
        }
      />
    </Card>
    <RolesSection client={rolesClient} />
    </>
  );
}

/// Действия над сотрудником в строке таблицы. Причина, по которой роль или отключение недоступны,
/// раньше жила во всплывающей подсказке — на неактивной кнопке браузер её не показывает. Теперь
/// это строка в ячейке. Обе кнопки гасит одно и то же правило (своя учётная запись, последний
/// администратор с полным доступом), поэтому одинаковую причину пишем один раз.
function AdminActions({ item, busy, roleReason, disableReason, onChangeRole, onToggleActive, onResetTwoFactor }: {
  item: PlatformAdminListItem;
  busy: boolean;
  roleReason: string | null;
  /// Только для активного сотрудника: включить обратно можно всегда.
  disableReason: string | null;
  onChangeRole: () => void;
  onToggleActive: () => void;
  onResetTwoFactor: () => void;
}) {
  const { t } = useI18n();
  const role = useBlockedReason(roleReason);
  const disable = useBlockedReason(disableReason === roleReason ? null : disableReason);
  const disableDescribedBy = disableReason === null ? undefined : disableReason === roleReason ? role.describedBy : disable.describedBy;

  return (
    <>
      <span className="pc-cell-actions">
        <Button
          size="sm"
          variant="outline"
          disabled={busy || roleReason !== null}
          aria-describedby={role.describedBy}
          onClick={onChangeRole}
        >
          {item.role === ROLE_PLATFORM_ADMIN ? t('platform.settings.action.makeSupport') : t('platform.settings.action.makeAdmin')}
        </Button>
        <Button
          size="sm"
          variant={item.isActive ? 'destructive' : 'outline'}
          disabled={busy || disableReason !== null}
          aria-describedby={disableDescribedBy}
          onClick={onToggleActive}
        >
          {item.isActive ? t('platform.settings.action.disable') : t('platform.settings.action.enable')}
        </Button>
        {item.twoFactorEnabled ? (
          <Button
            size="sm"
            variant="outline"
            disabled={busy}
            onClick={onResetTwoFactor}
          >
            {t('platform.settings.action.resetTwoFactor')}
          </Button>
        ) : null}
      </span>
      {role.hint}
      {disable.hint}
    </>
  );
}
