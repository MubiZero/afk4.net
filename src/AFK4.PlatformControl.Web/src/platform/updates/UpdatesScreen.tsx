import { cloneElement, useCallback, useEffect, useState, type FormEvent, type ReactElement } from 'react';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { UpdatesApi } from '@/api/platformClients/updates';
import type { PlatformUpdatePackage, PlatformUpdateRollout } from '@/api/types';
import { Page } from '@/components/layout/Page';
import { Badge, type BadgeVariant } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';

export type UpdatesClient = Pick<UpdatesApi, 'listPackages' | 'registerPackage' | 'changePackageState' | 'listRollouts' | 'createRollout' | 'changeRolloutState'>;
type OrganizationsClient = Pick<OrganizationsApi, 'listOrganizations'>;

// Раздел «Обновления»: сборка публикуется — и её получают ВСЕ клубы.
//
// Поэтапные выкатки с процентом партии и ручным выбором целей убраны из интерфейса сознательно:
// на нынешнем масштабе церемония дороже пользы, а разные версии у разных клубов — это лишний
// источник расхождений в поддержке. Механизм выкаток при этом жив: публикация создаёт выкатку
// на все организации сразу (100%), поэтому тревога «обновление не установилось» продолжает
// считаться по реальным отчётам устройств. Точечный рычаг остался один — закрепить версию
// конкретному клиенту в его карточке, когда у него что-то сломалось.
export function UpdatesScreen({ client, organizationsClient }: {
  client: UpdatesClient;
  organizationsClient: OrganizationsClient;
}) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [packages, setPackages] = useState<PlatformUpdatePackage[] | null>(null);
  const [rollouts, setRollouts] = useState<PlatformUpdateRollout[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [packageFormOpen, setPackageFormOpen] = useState(false);
  const [stateTarget, setStateTarget] = useState<{ id: string; state: string } | null>(null);
  const [publishTarget, setPublishTarget] = useState<PlatformUpdatePackage | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [rolloutAction, setRolloutAction] = useState<RolloutAction | null>(null);
  const [changingRollout, setChangingRollout] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [nextPackages, nextRollouts] = await Promise.all([client.listPackages(), client.listRollouts()]);
      setPackages(nextPackages);
      setRollouts(nextRollouts);
    } catch (cause) {
      setError(describeApiError(cause, t));
    }
  }, [client, t]);

  useEffect(() => { void load(); }, [load]);

  async function publish(target: PlatformUpdatePackage) {
    setPublishing(true);
    try {
      const organizations = await organizationsClient.listOrganizations();
      if (organizations.length === 0) {
        toast({ title: t('platform.updates.publish.noOrganizations'), variant: 'error' });
        return;
      }
      await client.createRollout({
        updatePackageId: target.updatePackageId,
        channel: target.channel,
        targetKind: 'organization',
        organizationIds: organizations.map(organization => organization.organizationId),
        branchIds: [],
        deviceIds: [],
        batchPercent: 100,
        startsAtUtc: new Date().toISOString(),
        reason: t('platform.updates.publish.reason', { version: target.version })
      });
      setPublishTarget(null);
      await load();
      toast({ title: t('platform.updates.publish.done'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPublishing(false);
    }
  }

  async function changeRolloutState(action: RolloutAction, reason: string) {
    setChangingRollout(true);
    try {
      await client.changeRolloutState(action.rollout.updateRolloutId, action.next, reason);
      setRolloutAction(null);
      await load();
      toast({ title: t('platform.updates.rollout.changed'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setChangingRollout(false);
    }
  }

  if (error !== null) return <Page title={t('nav.platform.updates')}><ErrorState message={error} retryLabel={t('common.retry')} onRetry={() => void load()} /></Page>;
  if (packages === null || rollouts === null) return <Page title={t('nav.platform.updates')}><LoadingCards count={3} /></Page>;

  // Список выкаток приходит от новых к старым, поэтому первая найденная для пакета — последняя.
  const rolloutByPackageId = new Map<string, PlatformUpdateRollout>();
  for (const rollout of rollouts) {
    if (!rolloutByPackageId.has(rollout.updatePackageId)) rolloutByPackageId.set(rollout.updatePackageId, rollout);
  }

  return (
    <Page
      title={t('nav.platform.updates')}
      description={t('platform.updates.packages.description')}
      actions={<Button onClick={() => setPackageFormOpen(true)}>{t('platform.updates.packages.register')}</Button>}
    >
      {packages.length === 0 ? <EmptyState message={t('platform.updates.packages.empty')} /> : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('platform.updates.col.component')}</TableHead>
              <TableHead>{t('platform.updates.col.version')}</TableHead>
              <TableHead>{t('platform.updates.col.channel')}</TableHead>
              <TableHead>{t('platform.updates.col.state')}</TableHead>
              <TableHead>{t('platform.updates.col.created')}</TableHead>
              <TableHead>{t('platform.updates.col.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {packages.map(row => (
              <TableRow key={row.updatePackageId}>
                <TableCell>{componentLabel(row.component)}</TableCell>
                <TableCell className="pc-num">{row.version}</TableCell>
                <TableCell>{row.channel}</TableCell>
                <TableCell>
                  <StateBadge state={row.state} />
                  <RolloutBadge rollout={rolloutByPackageId.get(row.updatePackageId)} />
                </TableCell>
                <TableCell>{formatDate(row.createdAtUtc)}</TableCell>
                <TableCell>
                  <span className="pc-cell-actions">
                    {row.state === 'registered' ? (
                      <Button size="sm" variant="outline" onClick={() => setStateTarget({ id: row.updatePackageId, state: 'validated' })}>
                        {t('platform.updates.package.validate')}
                      </Button>
                    ) : null}
                    {row.state === 'validated' && rolloutByPackageId.get(row.updatePackageId) === undefined ? (
                      <Button size="sm" onClick={() => setPublishTarget(row)}>{t('platform.updates.publish.action')}</Button>
                    ) : null}
                    <RolloutActions
                      rollout={rolloutByPackageId.get(row.updatePackageId)}
                      onAct={setRolloutAction}
                    />
                    {row.state === 'validated' ? (
                      <Button size="sm" variant="outline" onClick={() => setStateTarget({ id: row.updatePackageId, state: 'retired' })}>
                        {t('platform.updates.package.retire')}
                      </Button>
                    ) : null}
                  </span>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <PackageDialog open={packageFormOpen} onOpenChange={setPackageFormOpen} client={client} onSaved={load} />
      <StateDialog target={stateTarget} onOpenChange={open => { if (!open) setStateTarget(null); }} client={client} onSaved={load} />

      <Dialog
        open={publishTarget !== null}
        title={t('platform.updates.publish.title')}
        description={publishTarget !== null ? t('platform.updates.publish.description', { component: componentLabel(publishTarget.component), version: publishTarget.version }) : undefined}
        onClose={() => setPublishTarget(null)}
        footer={
          <>
            <Button variant="outline" disabled={publishing} onClick={() => setPublishTarget(null)}>{t('common.cancel')}</Button>
            <Button disabled={publishing} onClick={() => { if (publishTarget !== null) void publish(publishTarget); }}>
              {publishing ? t('common.saving') : t('platform.updates.publish.action')}
            </Button>
          </>
        }
      />

      <ConfirmDialog
        open={rolloutAction !== null}
        title={rolloutAction === null ? '' : t(ROLLOUT_ACTION_COPY[rolloutAction.next].title)}
        description={rolloutAction === null ? undefined : t(ROLLOUT_ACTION_COPY[rolloutAction.next].body)}
        confirmLabel={rolloutAction === null ? '' : t(ROLLOUT_ACTION_COPY[rolloutAction.next].confirm)}
        cancelLabel={t('common.cancel')}
        reasonLabel={t('platform.updates.rollout.reasonLabel')}
        destructive={rolloutAction?.next !== 'active'}
        pending={changingRollout}
        onConfirm={reason => { if (rolloutAction !== null) void changeRolloutState(rolloutAction, reason); }}
        onOpenChange={open => { if (!open) setRolloutAction(null); }}
      />
    </Page>
  );
}

// Три состояния, в которые раскатку переводит человек. Остальные значения сервера — итоговые
// (раскатана, откачена, отменена) и меняются не отсюда.
type RolloutNextState = 'paused' | 'active' | 'rollback-requested';

interface RolloutAction {
  rollout: PlatformUpdateRollout;
  next: RolloutNextState;
}

const ROLLOUT_ACTION_COPY: Record<RolloutNextState, { title: MessageKey; body: MessageKey; confirm: MessageKey }> = {
  paused: {
    title: 'platform.updates.rollout.pause.title',
    body: 'platform.updates.rollout.pause.body',
    confirm: 'platform.updates.rollout.pause.confirm'
  },
  active: {
    title: 'platform.updates.rollout.resume.title',
    body: 'platform.updates.rollout.resume.body',
    confirm: 'platform.updates.rollout.resume.confirm'
  },
  'rollback-requested': {
    title: 'platform.updates.rollout.rollback.title',
    body: 'platform.updates.rollout.rollback.body',
    confirm: 'platform.updates.rollout.rollback.confirm'
  }
};

const ROLLOUT_STATE_LABEL: Record<string, MessageKey> = {
  active: 'platform.updates.rollout.state.active',
  paused: 'platform.updates.rollout.state.paused',
  'rollback-requested': 'platform.updates.rollout.state.rollbackRequested',
  'rolled-back': 'platform.updates.rollout.state.rolledBack',
  completed: 'platform.updates.rollout.state.completed',
  cancelled: 'platform.updates.rollout.state.cancelled'
};

function RolloutBadge({ rollout }: { rollout: PlatformUpdateRollout | undefined }) {
  const { t } = useI18n();
  if (rollout === undefined) return null;
  const label = ROLLOUT_STATE_LABEL[rollout.state];
  // Незнакомое состояние показывается как есть: выдумать ему подпись нельзя, а промолчать —
  // значит спрятать выкатку, которая существует.
  if (label === undefined) return <Badge variant="outline">{rollout.state}</Badge>;
  const variant: BadgeVariant = rollout.state === 'active' ? 'success'
    : rollout.state === 'completed' ? 'success'
    : rollout.state === 'paused' || rollout.state === 'rollback-requested' ? 'warning'
    : 'outline';
  return <Badge variant={variant}>{t(label)}</Badge>;
}

function RolloutActions({ rollout, onAct }: {
  rollout: PlatformUpdateRollout | undefined;
  onAct: (action: RolloutAction) => void;
}) {
  const { t } = useI18n();
  if (rollout === undefined) return null;
  // Раскатана, откачена, отменена — итог; сервер их менять не даст, и кнопка обещала бы неправду.
  if (rollout.state !== 'active' && rollout.state !== 'paused') return null;

  return (
    <>
      {rollout.state === 'active' ? (
        <Button size="sm" variant="outline" onClick={() => onAct({ rollout, next: 'paused' })}>
          {t('platform.updates.rollout.pause')}
        </Button>
      ) : (
        <Button size="sm" variant="outline" onClick={() => onAct({ rollout, next: 'active' })}>
          {t('platform.updates.rollout.resume')}
        </Button>
      )}
      <Button size="sm" variant="destructive" onClick={() => onAct({ rollout, next: 'rollback-requested' })}>
        {t('platform.updates.rollout.rollback')}
      </Button>
    </>
  );
}

function PackageDialog({ open, onOpenChange, client, onSaved }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  client: UpdatesClient;
  onSaved: () => Promise<void>;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    try {
      await client.registerPackage({
        component: value(data, 'component'),
        version: value(data, 'version'),
        channel: value(data, 'channel'),
        artifactUri: value(data, 'artifactUri'),
        sha256: value(data, 'sha256'),
        signature: value(data, 'signature'),
        signatureAlgorithm: 'ecdsa-p256-sha256-ieee-p1363',
        sizeBytes: Number(value(data, 'sizeBytes')),
        releaseNotes: value(data, 'releaseNotes')
      });
      onOpenChange(false);
      await onSaved();
      toast({ title: t('platform.updates.package.registered'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} title={t('platform.updates.packages.register')} description={t('platform.updates.packages.formHint')} onClose={() => onOpenChange(false)}>
      <form className="mgmt-form" onSubmit={submit}>
        <div className="mgmt-form-grid">
          <NativeField label={t('platform.updates.field.component')} name="component">
            <select name="component" className="ui-select" defaultValue="organization_admin">
              <option value="organization_admin">Organization Admin</option>
              <option value="operator_app">Admin App</option>
              <option value="agent_service">Agent Service</option>
              <option value="player_shell">Player Shell</option>
            </select>
          </NativeField>
          <NativeField label={t('platform.updates.field.channel')} name="channel">
            <select name="channel" className="ui-select" defaultValue="stable">
              <option value="stable">stable</option>
              <option value="beta">beta</option>
            </select>
          </NativeField>
          <NativeField label={t('platform.updates.field.version')} name="version"><Input name="version" required /></NativeField>
          <NativeField label={t('platform.updates.field.size')} name="sizeBytes"><Input name="sizeBytes" type="number" min="1" required /></NativeField>
        </div>
        <NativeField label={t('platform.updates.field.artifact')} name="artifactUri"><Input name="artifactUri" type="url" required /></NativeField>
        <NativeField label="SHA-256" name="sha256"><Input name="sha256" minLength={64} maxLength={64} required /></NativeField>
        <NativeField label={t('platform.updates.field.signature')} name="signature"><Textarea name="signature" required /></NativeField>
        <NativeField label={t('platform.updates.field.notes')} name="releaseNotes"><Textarea name="releaseNotes" required /></NativeField>
        <div className="mgmt-form-actions">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button type="submit" disabled={busy}>{busy ? t('common.saving') : t('platform.updates.packages.register')}</Button>
        </div>
      </form>
    </Dialog>
  );
}

function StateDialog({ target, onOpenChange, client, onSaved }: {
  target: { id: string; state: string } | null;
  onOpenChange: (open: boolean) => void;
  client: UpdatesClient;
  onSaved: () => Promise<void>;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (target === null) return;
    setBusy(true);
    try {
      await client.changePackageState(target.id, target.state, reason);
      onOpenChange(false);
      setReason('');
      await onSaved();
      toast({ title: t('platform.updates.state.changed'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog
      open={target !== null}
      title={target?.state === 'validated' ? t('platform.updates.package.validate') : t('platform.updates.state.change')}
      description={t('platform.updates.state.reasonHint')}
      onClose={() => onOpenChange(false)}
    >
      <form className="mgmt-form" onSubmit={submit}>
        <NativeField label={t('platform.updates.field.reason')} name="reason">
          <Textarea aria-label={t('platform.updates.field.reason')} value={reason} onChange={event => setReason(event.target.value)} required />
        </NativeField>
        <div className="mgmt-form-actions">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button type="submit" disabled={busy}>
            {target?.state === 'validated' ? t('platform.updates.package.confirmValidation') : t('platform.updates.state.confirm')}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

function NativeField({ label, name, children }: { label: string; name: string; children: ReactElement<{ id?: string }> }) {
  return (
    <label className="ui-field" htmlFor={name}>
      <span>{label}</span>
      {cloneElement(children, { id: name })}
    </label>
  );
}

function value(data: FormData, key: string): string {
  return String(data.get(key) ?? '').trim();
}

function componentLabel(value: string): string {
  return ({
    organization_admin: 'Organization Admin',
    operator_app: 'Admin App',
    agent_service: 'Agent Service',
    player_shell: 'Player Shell'
  } as Record<string, string>)[value] ?? value;
}



// Состояние приходит с сервера машинным словом. Печатать его как есть значило показывать
// «registered» посреди русского экрана — это не термин и не бренд, а непереведённая строка.
const STATE_LABELS: Record<string, MessageKey> = {
  registered: 'platform.updates.state.registered',
  validated: 'platform.updates.state.validated',
  rejected: 'platform.updates.state.rejected',
  retired: 'platform.updates.state.retired'
};

function StateBadge({ state }: { state: string }) {
  const { t } = useI18n();
  const variant: BadgeVariant = state === 'validated' ? 'success' : state === 'rejected' || state === 'retired' ? 'destructive' : 'outline';
  const label = STATE_LABELS[state];
  // Незнакомое состояние показываем как есть: молча спрятать его хуже, чем показать сырым.
  return <Badge variant={variant}>{label ? t(label) : state}</Badge>;
}
