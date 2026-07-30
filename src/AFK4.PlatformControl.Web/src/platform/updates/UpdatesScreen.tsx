import { cloneElement, useCallback, useEffect, useState, type FormEvent, type ReactElement, type ReactNode } from 'react';
import type { UpdatesApi } from '@/api/platformClients/updates';
import type { PlatformUpdatePackage, PlatformUpdateRollout } from '@/api/types';
import { Badge, type BadgeVariant } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';

type Client = Pick<UpdatesApi, 'listPackages' | 'registerPackage' | 'changePackageState' | 'listRollouts' | 'createRollout' | 'changeRolloutState'>;

export function UpdatesScreen({ client, tab = 'packages', onTabChange = () => {} }: { client: Client; tab?: 'packages' | 'rollouts'; onTabChange?: (tab: 'packages' | 'rollouts') => void }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [packages, setPackages] = useState<PlatformUpdatePackage[] | null>(null);
  const [rollouts, setRollouts] = useState<PlatformUpdateRollout[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [packageFormOpen, setPackageFormOpen] = useState(false);
  const [rolloutFormOpen, setRolloutFormOpen] = useState(false);
  const [stateTarget, setStateTarget] = useState<{ kind: 'package' | 'rollout'; id: string; state: string } | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [nextPackages, nextRollouts] = await Promise.all([client.listPackages(), client.listRollouts()]);
      setPackages(nextPackages);
      setRollouts(nextRollouts);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('platform.updates.loadError'));
    }
  }, [client, t]);

  useEffect(() => { void load(); }, [load]);

  if (error !== null) return <ErrorState message={error} retryLabel={t('common.retry')} onRetry={() => void load()} />;
  if (packages === null || rollouts === null) return <LoadingCards count={3} />;

  return (
    <Tabs value={tab} onValueChange={value => onTabChange(value as typeof tab)} className="flex flex-col gap-4">
      <TabsList>
        <TabsTrigger value="packages">{t('platform.updates.tab.packages')}</TabsTrigger>
        <TabsTrigger value="rollouts">{t('platform.updates.tab.rollouts')}</TabsTrigger>
      </TabsList>
      <TabsContent value="packages">
        <SectionHeader title={t('platform.updates.packages.title')} description={t('platform.updates.packages.description')}>
          <Button onClick={() => setPackageFormOpen(true)}>{t('platform.updates.packages.register')}</Button>
        </SectionHeader>
        {packages.length === 0 ? <EmptyState message={t('platform.updates.packages.empty')} /> : (
          <Table>
            <TableHeader><TableRow>
              <TableHead>{t('platform.updates.col.component')}</TableHead><TableHead>{t('platform.updates.col.version')}</TableHead>
              <TableHead>{t('platform.updates.col.channel')}</TableHead><TableHead>{t('platform.updates.col.state')}</TableHead>
              <TableHead>{t('platform.updates.col.created')}</TableHead><TableHead className="text-right">{t('platform.updates.col.actions')}</TableHead>
            </TableRow></TableHeader>
            <TableBody>{packages.map(row => (
              <TableRow key={row.updatePackageId}>
                <TableCell className="font-medium">{componentLabel(row.component)}</TableCell>
                <TableCell className="font-mono tabular-nums">{row.version}</TableCell><TableCell>{row.channel}</TableCell>
                <TableCell><StateBadge state={row.state} /></TableCell><TableCell>{formatDate(row.createdAtUtc)}</TableCell>
                <TableCell className="text-right">
                  {row.state === 'registered' && <Button size="sm" variant="outline" onClick={() => setStateTarget({ kind: 'package', id: row.updatePackageId, state: 'validated' })}>{t('platform.updates.package.validate')}</Button>}
                  {row.state === 'validated' && <Button size="sm" variant="outline" onClick={() => setStateTarget({ kind: 'package', id: row.updatePackageId, state: 'retired' })}>{t('platform.updates.package.retire')}</Button>}
                </TableCell>
              </TableRow>
            ))}</TableBody>
          </Table>
        )}
      </TabsContent>
      <TabsContent value="rollouts">
        <SectionHeader title={t('platform.updates.rollouts.title')} description={t('platform.updates.rollouts.description')}>
          <Button onClick={() => setRolloutFormOpen(true)}>{t('platform.updates.rollouts.create')}</Button>
        </SectionHeader>
        {rollouts.length === 0 ? <EmptyState message={t('platform.updates.rollouts.empty')} /> : (
          <Table>
            <TableHeader><TableRow>
              <TableHead>{t('platform.updates.col.release')}</TableHead><TableHead>{t('platform.updates.col.scope')}</TableHead>
              <TableHead>{t('platform.updates.col.batch')}</TableHead><TableHead>{t('platform.updates.col.state')}</TableHead>
              <TableHead>{t('platform.updates.col.starts')}</TableHead><TableHead className="text-right">{t('platform.updates.col.actions')}</TableHead>
            </TableRow></TableHeader>
            <TableBody>{rollouts.map(row => (
              <TableRow key={row.updateRolloutId}>
                <TableCell><span className="font-medium">{componentLabel(row.component)}</span> <span className="font-mono">{row.version}</span></TableCell>
                <TableCell>{row.targetKind} ({targetCount(row)})</TableCell><TableCell className="tabular-nums">{row.batchPercent}%</TableCell>
                <TableCell><StateBadge state={row.state} /></TableCell><TableCell>{formatDate(row.startsAtUtc)}</TableCell>
                <TableCell className="text-right">{row.state === 'active' && <Button size="sm" variant="outline" onClick={() => setStateTarget({ kind: 'rollout', id: row.updateRolloutId, state: 'paused' })}>{t('platform.updates.rollout.pause')}</Button>}</TableCell>
              </TableRow>
            ))}</TableBody>
          </Table>
        )}
      </TabsContent>
      <PackageDialog open={packageFormOpen} onOpenChange={setPackageFormOpen} client={client} onSaved={load} />
      <RolloutDialog open={rolloutFormOpen} onOpenChange={setRolloutFormOpen} client={client} packages={packages.filter(row => row.state === 'validated')} onSaved={load} />
      <StateDialog target={stateTarget} onOpenChange={open => { if (!open) setStateTarget(null); }} client={client} onSaved={load} />
    </Tabs>
  );
}

function SectionHeader({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return <div className="mb-4 flex items-start justify-between gap-4"><div><h2 className="text-lg font-bold">{title}</h2><p className="mt-1 max-w-[66ch] text-sm text-muted-foreground">{description}</p></div>{children}</div>;
}

function PackageDialog({ open, onOpenChange, client, onSaved }: { open: boolean; onOpenChange: (open: boolean) => void; client: Client; onSaved: () => Promise<void> }) {
  const { t } = useI18n(); const { toast } = useToast(); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget); setBusy(true);
    try {
      await client.registerPackage({ component: value(data, 'component'), version: value(data, 'version'), channel: value(data, 'channel'), artifactUri: value(data, 'artifactUri'), sha256: value(data, 'sha256'), signature: value(data, 'signature'), signatureAlgorithm: 'ecdsa-p256-sha256-ieee-p1363', sizeBytes: Number(value(data, 'sizeBytes')), releaseNotes: value(data, 'releaseNotes') });
      onOpenChange(false); await onSaved(); toast({ title: t('platform.updates.package.registered'), variant: 'success' });
    } catch (cause) { toast({ title: cause instanceof Error ? cause.message : t('platform.updates.saveError'), variant: 'error' }); } finally { setBusy(false); }
  }
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="max-h-[90dvh] max-w-2xl overflow-y-auto"><DialogTitle>{t('platform.updates.packages.register')}</DialogTitle><DialogDescription>{t('platform.updates.packages.formHint')}</DialogDescription><form className="mt-4 grid grid-cols-2 gap-3" onSubmit={submit}>
    <NativeField label={t('platform.updates.field.component')} name="component"><select name="component" className="input" defaultValue="organization_admin"><option value="organization_admin">Organization Admin</option><option value="operator_app">Admin App</option><option value="agent_service">Agent Service</option><option value="player_shell">Player Shell</option></select></NativeField>
    <NativeField label={t('platform.updates.field.channel')} name="channel"><select name="channel" className="input" defaultValue="stable"><option value="stable">stable</option><option value="beta">beta</option></select></NativeField>
    <NativeField label={t('platform.updates.field.version')} name="version"><Input name="version" required /></NativeField><NativeField label={t('platform.updates.field.size')} name="sizeBytes"><Input name="sizeBytes" type="number" min="1" required /></NativeField>
    <div className="col-span-2"><NativeField label={t('platform.updates.field.artifact')} name="artifactUri"><Input name="artifactUri" type="url" required /></NativeField></div>
    <div className="col-span-2"><NativeField label="SHA-256" name="sha256"><Input name="sha256" minLength={64} maxLength={64} required /></NativeField></div>
    <div className="col-span-2"><NativeField label={t('platform.updates.field.signature')} name="signature"><Textarea name="signature" required /></NativeField></div>
    <div className="col-span-2"><NativeField label={t('platform.updates.field.notes')} name="releaseNotes"><Textarea name="releaseNotes" required /></NativeField></div>
    <DialogFooter className="col-span-2"><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{busy ? t('common.saving') : t('platform.updates.packages.register')}</Button></DialogFooter>
  </form></DialogContent></Dialog>;
}

function RolloutDialog({ open, onOpenChange, client, packages, onSaved }: { open: boolean; onOpenChange: (open: boolean) => void; client: Client; packages: PlatformUpdatePackage[]; onSaved: () => Promise<void> }) {
  const { t } = useI18n(); const { toast } = useToast(); const [busy, setBusy] = useState(false); const [kind, setKind] = useState('organization');
  async function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); const data = new FormData(event.currentTarget); const ids = value(data, 'targets').split(',').map(item => item.trim()).filter(Boolean); setBusy(true); try { const selected = packages.find(item => item.updatePackageId === value(data, 'packageId'))!; await client.createRollout({ updatePackageId: selected.updatePackageId, channel: selected.channel, targetKind: kind, organizationIds: kind === 'organization' ? ids : [], branchIds: kind === 'branch' ? ids : [], deviceIds: kind === 'device' ? ids : [], batchPercent: Number(value(data, 'batchPercent')), startsAtUtc: new Date(value(data, 'startsAtUtc')).toISOString(), reason: value(data, 'reason') }); onOpenChange(false); await onSaved(); toast({ title: t('platform.updates.rollout.created'), variant: 'success' }); } catch (cause) { toast({ title: cause instanceof Error ? cause.message : t('platform.updates.saveError'), variant: 'error' }); } finally { setBusy(false); } }
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent><DialogTitle>{t('platform.updates.rollouts.create')}</DialogTitle><DialogDescription>{t('platform.updates.rollouts.formHint')}</DialogDescription><form className="mt-4 flex flex-col gap-3" onSubmit={submit}>
    <NativeField label={t('platform.updates.field.package')} name="packageId"><select name="packageId" className="input" required>{packages.map(item => <option key={item.updatePackageId} value={item.updatePackageId}>{componentLabel(item.component)} {item.version} ({item.channel})</option>)}</select></NativeField>
    <NativeField label={t('platform.updates.field.targetKind')} name="targetKind"><select name="targetKind" className="input" value={kind} onChange={event => setKind(event.target.value)}><option value="organization">{t('platform.updates.target.organization')}</option><option value="branch">{t('platform.updates.target.branch')}</option><option value="device">{t('platform.updates.target.device')}</option></select></NativeField>
    <NativeField label={t('platform.updates.field.targets')} name="targets"><Textarea name="targets" required /></NativeField>
    <NativeField label={t('platform.updates.field.batch')} name="batchPercent"><Input name="batchPercent" type="number" min="1" max="100" defaultValue="10" required /></NativeField>
    <NativeField label={t('platform.updates.field.starts')} name="startsAtUtc"><Input name="startsAtUtc" type="datetime-local" required /></NativeField>
    <NativeField label={t('platform.updates.field.reason')} name="reason"><Textarea name="reason" required /></NativeField>
    <DialogFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy || packages.length === 0}>{busy ? t('common.saving') : t('platform.updates.rollouts.create')}</Button></DialogFooter>
  </form></DialogContent></Dialog>;
}

function StateDialog({ target, onOpenChange, client, onSaved }: { target: { kind: 'package' | 'rollout'; id: string; state: string } | null; onOpenChange: (open: boolean) => void; client: Client; onSaved: () => Promise<void> }) {
  const { t } = useI18n(); const { toast } = useToast(); const [reason, setReason] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); if (target === null) return; setBusy(true); try { if (target.kind === 'package') await client.changePackageState(target.id, target.state, reason); else await client.changeRolloutState(target.id, target.state, reason); onOpenChange(false); setReason(''); await onSaved(); toast({ title: t('platform.updates.state.changed'), variant: 'success' }); } catch (cause) { toast({ title: cause instanceof Error ? cause.message : t('platform.updates.saveError'), variant: 'error' }); } finally { setBusy(false); } }
  return <Dialog open={target !== null} onOpenChange={onOpenChange}><DialogContent><DialogTitle>{target?.state === 'validated' ? t('platform.updates.package.validate') : t('platform.updates.state.change')}</DialogTitle><DialogDescription>{t('platform.updates.state.reasonHint')}</DialogDescription><form className="mt-4" onSubmit={submit}><NativeField label={t('platform.updates.field.reason')} name="reason"><Textarea aria-label={t('platform.updates.field.reason')} value={reason} onChange={event => setReason(event.target.value)} required /></NativeField><DialogFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button><Button type="submit" disabled={busy}>{target?.state === 'validated' ? t('platform.updates.package.confirmValidation') : t('platform.updates.state.confirm')}</Button></DialogFooter></form></DialogContent></Dialog>;
}

function NativeField({ label, name, children }: { label: string; name: string; children: ReactElement<{ id?: string }> }) { return <label className="block" htmlFor={name}><span className="mb-1 block text-sm text-muted-foreground">{label}</span>{cloneElement(children, { id: name })}</label>; }
function value(data: FormData, key: string): string { return String(data.get(key) ?? '').trim(); }
function componentLabel(value: string): string { return ({ organization_admin: 'Organization Admin', operator_app: 'Admin App', agent_service: 'Agent Service', player_shell: 'Player Shell' } as Record<string, string>)[value] ?? value; }
function targetCount(row: PlatformUpdateRollout): number { return row.organizationIds.length + row.branchIds.length + row.deviceIds.length; }
function formatDate(value: string): string { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
function StateBadge({ state }: { state: string }) { const variant: BadgeVariant = state === 'validated' || state === 'active' || state === 'completed' ? 'success' : state === 'rejected' || state === 'cancelled' ? 'destructive' : 'outline'; return <Badge variant={variant}>{state}</Badge>; }
