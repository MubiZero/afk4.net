import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonTable } from '@/components/ui/skeletons';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useBlockedReason } from '@/components/ui/blockedReason';
import { describeApiError } from '@/api/describeApiError';
import { organizationOwnerActivationUrl } from './organizationsModel';
import { AccessCodeHandoff } from '@/components/shared/AccessCodeHandoff';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationOwnerInvitesApi } from '@/api/platformClients/organizationOwnerInvites';
import type { OrganizationOwnerInvite, OrganizationBranch } from '@/api/types';
import { useLoadable } from '../useLoadable';
import { INVITE_STATUS_VARIANT, INVITE_STATUS_LABEL } from './organizationsModel';

type Client = Pick<OrganizationOwnerInvitesApi, 'listOrganizationOwnerInvites' | 'createOrganizationOwnerInvite' | 'revokeOrganizationOwnerInvite'>;

interface Props {
  client: Client;
  organizationId: string;
  branches: OrganizationBranch[];
  initialInvite?: OrganizationOwnerInvite | null;
}

export function OrganizationOwnerInvitesSection({ client, organizationId, branches, initialInvite }: Props) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useLoadable(() => client.listOrganizationOwnerInvites(organizationId), [organizationId]);
  const [revealed, setRevealed] = useState<Map<string, string>>(() => {
    const seed = new Map<string, string>();
    if (initialInvite) seed.set(initialInvite.organizationOwnerInviteId, initialInvite.code);
    return seed;
  });
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const [ownerUserName, setOwnerUserName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [ownerEmail, setOwnerEmail] = useState('');
  const [creating, setCreating] = useState(false);
  // Выданный код надо передать владельцу целиком и без опечаток. Раньше он появлялся строкой в
  // таблице, и единственным способом было выделить его мышью из ячейки; ошибся — код не показать
  // второй раз, надо отзывать и выдавать новый.
  const [handoff, setHandoff] = useState<string | null>(null);
  const [revokeId, setRevokeId] = useState<string | null>(null);
  const [revoking, setRevoking] = useState(false);
  // Код выдаётся на филиал. У организации без филиала список пуст, и серая кнопка без слов не
  // говорила, что сначала нужен филиал.
  const noBranch = useBlockedReason(branchId === '' ? t('platform.organization.invites.blocked.noBranch') : null);

  async function create() {
    if (branchId === '') return;
    setCreating(true);
    try {
      const made = await client.createOrganizationOwnerInvite(
        organizationId,
        branchId,
        ownerUserName.trim() === '' ? null : ownerUserName.trim(),
        ownerDisplayName.trim() === '' ? null : ownerDisplayName.trim(),
        null,
        ownerEmail.trim() === '' ? null : ownerEmail.trim()
      );
      setRevealed(cur => new Map(cur).set(made.organizationOwnerInviteId, made.code));
      setHandoff(made.code);
      setOwnerUserName(''); setOwnerDisplayName(''); setOwnerEmail('');
      toast({ title: t('platform.organization.invites.created'), variant: 'success' });
      state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setCreating(false);
    }
  }

  async function revoke(reason: string) {
    if (revokeId === null) return;
    setRevoking(true);
    try {
      await client.revokeOrganizationOwnerInvite(revokeId, reason);
      setRevealed(cur => { const next = new Map(cur); next.delete(revokeId); return next; });
      setRevokeId(null);
      toast({ title: t('platform.organization.invites.revoked'), variant: 'success' });
      state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setRevoking(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.section.invites')}</CardTitle></CardHeader>
      <CardContent>
        <div>
          <label className="ui-field">
            <span>{t('platform.organization.invites.branch')}</span>
            <Select value={branchId} onChange={event => setBranchId(event.target.value)}>
                {branches.map(b => <option key={b.branchId} value={b.branchId}>{b.name} ({b.city})</option>)}
            </Select>
          </label>
          <label className="ui-field">
            <span>{t('platform.organization.invites.ownerUserName')}</span>
            <Input aria-label={t('platform.organization.invites.ownerUserName')} value={ownerUserName} onChange={e => setOwnerUserName(e.target.value)} />
          </label>
          <label className="ui-field">
            <span>{t('platform.organization.invites.ownerDisplayName')}</span>
            <Input aria-label={t('platform.organization.invites.ownerDisplayName')} value={ownerDisplayName} onChange={e => setOwnerDisplayName(e.target.value)} />
          </label>
          <label className="ui-field">
            <span>{t('platform.organization.invites.ownerEmail')}</span>
            <Input
              type="email"
              aria-label={t('platform.organization.invites.ownerEmail')}
              value={ownerEmail}
              onChange={e => setOwnerEmail(e.target.value)}
            />
            <small>{t('platform.organization.invites.ownerEmailHint')}</small>
          </label>
          <div>
            <Button onClick={() => void create()} disabled={creating || branchId === ''} aria-describedby={noBranch.describedBy}>{t('platform.organization.invites.create')}</Button>
            {noBranch.hint}
          </div>
        </div>

        {handoff !== null ? (
          <div className="mgmt-form">
            <AccessCodeHandoff
              code={handoff}
              activationUrl={organizationOwnerActivationUrl(window.location.origin, handoff)}
              idPrefix="owner-invite"
            />
          </div>
        ) : null}

        {state.status === 'error' ? (
          <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />
        ) : state.status === 'loading' ? (
          <Loading><SkeletonTable columns={5} rows={2} /></Loading>
        ) : state.data.length === 0 ? (
          // Без филиала форма выше кода не создаст — звать к ней значило бы обещать то, чего нет.
          branchId === ''
            ? <EmptyState message={t('platform.organization.invites.emptyNoBranch')} next="elsewhere" />
            : <EmptyState message={t('platform.organization.invites.empty')} next="formAbove" />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.organization.invites.colStatus')}</TableHead>
                <TableHead>{t('platform.organization.invites.colCode')}</TableHead>
                <TableHead>{t('platform.organization.invites.colOwner')}</TableHead>
                <TableHead>{t('platform.organization.invites.colExpires')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {state.data.map(inv => {
                const code = revealed.get(inv.organizationOwnerInviteId);
                return (
                  <TableRow key={inv.organizationOwnerInviteId}>
                    <TableCell>
                      <Badge variant={INVITE_STATUS_VARIANT[inv.status] ?? 'outline'}>
                        {INVITE_STATUS_LABEL[inv.status] ? t(INVITE_STATUS_LABEL[inv.status]) : inv.status}
                      </Badge>
                    </TableCell>
                    <TableCell><code className="pc-mono">{code !== undefined ? code : `•••• ${inv.codeSuffix}`}</code></TableCell>
                    <TableCell>{inv.ownerUserName ?? '—'}</TableCell>
                    <TableCell className="pc-num">{formatDate(inv.expiresAtUtc)}</TableCell>
                    <TableCell className="pc-num">
                      {inv.status === 'pending' && (
                        <Button variant="ghost" size="sm" onClick={() => setRevokeId(inv.organizationOwnerInviteId)}>
                          {t('platform.organization.invites.revoke')}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <ConfirmDialog
        open={revokeId !== null}
        title={t('platform.organization.invites.revokeTitle')}
        confirmLabel={t('platform.organization.invites.revokeConfirm')}
        cancelLabel={t('platform.organization.statusForm.cancel')}
        reasonLabel={t('platform.organization.invites.revokeReason')}
        destructive
        pending={revoking}
        onConfirm={reason => void revoke(reason)}
        onOpenChange={open => { if (!open) setRevokeId(null); }}
      />
    </Card>
  );
}
