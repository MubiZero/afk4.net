import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationOwnerInvitesApi } from '@/api/platformClients/organizationOwnerInvites';
import type { OrganizationOwnerInvite, OrganizationOwnerInviteSummary, TenantBranch } from '@/api/types';
import { INVITE_STATUS_VARIANT, INVITE_STATUS_LABEL } from './tenantsModel';

type Client = Pick<OrganizationOwnerInvitesApi, 'listOrganizationOwnerInvites' | 'createOrganizationOwnerInvite' | 'revokeOrganizationOwnerInvite'>;

interface Props {
  client: Client;
  organizationId: string;
  branches: TenantBranch[];
  initialInvite?: OrganizationOwnerInvite | null;
}

export function TenantOrganizationOwnerInvitesSection({ client, organizationId, branches, initialInvite }: Props) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [invites, setInvites] = useState<OrganizationOwnerInviteSummary[] | null>(null);
  const [error, setError] = useState(false);
  const [revealed, setRevealed] = useState<Map<string, string>>(() => {
    const seed = new Map<string, string>();
    if (initialInvite) seed.set(initialInvite.organizationOwnerInviteId, initialInvite.code);
    return seed;
  });
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const [ownerUserName, setOwnerUserName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [creating, setCreating] = useState(false);
  const [revokeId, setRevokeId] = useState<string | null>(null);
  const [revoking, setRevoking] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setInvites(null); setError(false);
    client.listOrganizationOwnerInvites(organizationId)
      .then(rows => { if (!cancelled) setInvites(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function create() {
    if (branchId === '') return;
    setCreating(true);
    try {
      const made = await client.createOrganizationOwnerInvite(
        organizationId,
        branchId,
        ownerUserName.trim() === '' ? null : ownerUserName.trim(),
        ownerDisplayName.trim() === '' ? null : ownerDisplayName.trim(),
        null
      );
      setRevealed(cur => new Map(cur).set(made.organizationOwnerInviteId, made.code));
      setOwnerUserName(''); setOwnerDisplayName('');
      toast({ title: t('platform.tenant.invites.created'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
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
      toast({ title: t('platform.tenant.invites.revoked'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setRevoking(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.invites')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        <div className="flex flex-col gap-3">
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.branch')}</span>
            <Select value={branchId} onValueChange={setBranchId}>
              <SelectTrigger aria-label={t('platform.tenant.invites.branch')}><SelectValue /></SelectTrigger>
              <SelectContent>
                {branches.map(b => <SelectItem key={b.branchId} value={b.branchId}>{b.name} ({b.city})</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.ownerUserName')}</span>
            <Input aria-label={t('platform.tenant.invites.ownerUserName')} value={ownerUserName} onChange={e => setOwnerUserName(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.ownerDisplayName')}</span>
            <Input aria-label={t('platform.tenant.invites.ownerDisplayName')} value={ownerDisplayName} onChange={e => setOwnerDisplayName(e.target.value)} />
          </label>
          <div>
            <Button onClick={() => void create()} disabled={creating || branchId === ''}>{t('platform.tenant.invites.create')}</Button>
          </div>
        </div>

        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : invites === null ? (
          <LoadingCards count={1} />
        ) : invites.length === 0 ? (
          <EmptyState message={t('platform.tenant.invites.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.tenant.invites.colStatus')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colCode')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colOwner')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colExpires')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {invites.map(inv => {
                const code = revealed.get(inv.organizationOwnerInviteId);
                return (
                  <TableRow key={inv.organizationOwnerInviteId}>
                    <TableCell>
                      <Badge variant={INVITE_STATUS_VARIANT[inv.status] ?? 'outline'}>
                        {INVITE_STATUS_LABEL[inv.status] ? t(INVITE_STATUS_LABEL[inv.status]) : inv.status}
                      </Badge>
                    </TableCell>
                    <TableCell><code className="font-mono text-xs">{code !== undefined ? code : `•••• ${inv.codeSuffix}`}</code></TableCell>
                    <TableCell>{inv.ownerUserName ?? '—'}</TableCell>
                    <TableCell className="tabular-nums">{formatDate(inv.expiresAtUtc)}</TableCell>
                    <TableCell className="text-right">
                      {inv.status === 'pending' && (
                        <Button variant="ghost" size="sm" onClick={() => setRevokeId(inv.organizationOwnerInviteId)}>
                          {t('platform.tenant.invites.revoke')}
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
        title={t('platform.tenant.invites.revokeTitle')}
        confirmLabel={t('platform.tenant.invites.revokeConfirm')}
        cancelLabel={t('platform.tenant.statusForm.cancel')}
        reasonLabel={t('platform.tenant.invites.revokeReason')}
        destructive
        pending={revoking}
        onConfirm={reason => void revoke(reason)}
        onOpenChange={open => { if (!open) setRevokeId(null); }}
      />
    </Card>
  );
}
