import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { Dialog } from '@/components/ui/dialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdminsApi } from '@/api/platformClients/admins';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import type { PlatformAdminInvitation, PlatformAdminListItem } from '@/api/types';
import { useAdmins } from './useAdmins';
import { AdminInviteDialog } from './AdminInviteDialog';
import {
  ROLE_PLATFORM_ADMIN,
  canDisable,
  changeRoleBlockReasonKey,
  describeAdminActionError,
  disableBlockReasonKey,
  roleLabelKey
} from './adminsModel';

export function SettingsScreen({ client, session }: { client: AdminsApi; session: PlatformAdminSession }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useAdmins(client);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [revokeTarget, setRevokeTarget] = useState<PlatformAdminInvitation | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  function refresh() {
    if (state.status === 'ready') state.retry();
  }

  async function toggleActive(item: PlatformAdminListItem) {
    setBusyId(item.platformAdminUserId);
    try {
      await client.updateAdmin(item.platformAdminUserId, { isActive: !item.isActive });
      toast({ title: item.isActive ? t('platform.settings.disabled') : t('platform.settings.enabled'), variant: 'success' });
      refresh();
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
      refresh();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setBusyId(null);
    }
  }

  async function revoke(invitation: PlatformAdminInvitation) {
    setBusyId(invitation.invitationId);
    try {
      await client.revokeInvitation(invitation.invitationId);
      toast({ title: t('platform.settings.invite.revoked'), variant: 'success' });
      setRevokeTarget(null);
      refresh();
    } catch (cause) {
      toast({ title: describeAdminActionError(cause, t), variant: 'error' });
    } finally {
      setBusyId(null);
    }
  }

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { admins, invitations } = state;
  const pendingInvitations = invitations.filter(invitation => invitation.status === 'pending');
  const isEmpty = admins.length === 0 && pendingInvitations.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.settings.title')}</CardTitle>
        <Button onClick={() => setInviteOpen(true)}>{t('platform.settings.action.invite')}</Button>
      </CardHeader>
      <CardContent>
        {isEmpty ? (
          <EmptyState message={t('platform.settings.empty')} />
        ) : (
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
                      <span className="pc-cell-actions">
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy || roleReason !== null}
                          title={roleReason !== null ? t(roleReason) : undefined}
                          onClick={() => void toggleRole(item)}
                        >
                          {item.role === ROLE_PLATFORM_ADMIN ? t('platform.settings.action.makeSupport') : t('platform.settings.action.makeAdmin')}
                        </Button>
                        <Button
                          size="sm"
                          variant={item.isActive ? 'destructive' : 'outline'}
                          disabled={busy || (item.isActive && !canDisable(item, session.platformAdminId, admins))}
                          title={item.isActive && disableReason !== null ? t(disableReason) : undefined}
                          onClick={() => void toggleActive(item)}
                        >
                          {item.isActive ? t('platform.settings.action.disable') : t('platform.settings.action.enable')}
                        </Button>
                      </span>
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
        )}
      </CardContent>

      <AdminInviteDialog open={inviteOpen} client={client} onOpenChange={setInviteOpen} onCreated={refresh} />

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
  );
}
