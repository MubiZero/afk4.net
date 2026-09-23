import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, PartialFailure } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { alertDetailText, alertLabel } from '@/platform/clubs/pulseModel';
import { NewBranchDialog } from './NewBranchDialog';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { PulseApi } from '@/api/platformClients/pulse';
import type { OrganizationBranch, OrganizationLimits, PulseClub } from '@/api/types';

type PulseClient = Pick<PulseApi, 'getPulse'>;
type OrganizationsClient = Pick<OrganizationsApi, 'createBranch'>;

/**
 * The branch roster is structural truth (organization detail); the pulse only
 * adds a live overlay (devices/seats/shift) when a branch has reported in.
 * A pulse outage must not hide the roster itself.
 */
export function OrganizationClubsTab({ client, organizationsClient, organizationId, branches, limits, canAddBranch, onBranchCreated }: {
  client: PulseClient;
  organizationsClient: OrganizationsClient;
  organizationId: string;
  branches: OrganizationBranch[];
  limits: OrganizationLimits;
  /// Филиал сервер заводит по праву на заведение организаций. Без него кнопки нет вовсе: на неё
  /// ответили бы только отказом.
  canAddBranch: boolean;
  onBranchCreated: (branch: OrganizationBranch) => void;
}) {
  const { t, formatDate } = useI18n();
  const [tick, setTick] = useState(0);
  const [pulseByBranch, setPulseByBranch] = useState<Map<string, PulseClub> | null>(null);
  const [error, setError] = useState(false);
  const [addOpen, setAddOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setError(false);
    client.getPulse()
      .then(pulse => {
        if (cancelled) return;
        const organization = pulse.organizations.find(o => o.organizationId === organizationId);
        setPulseByBranch(new Map((organization?.clubs ?? []).map(club => [club.branchId, club])));
      })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  const header = (
    <div className="pc-cell-actions">
      {canAddBranch ? <Button size="sm" onClick={() => setAddOpen(true)}>{t('platform.organization.branches.add')}</Button> : null}
      {limits.maxBranches !== null ? (
        <span>{t('platform.organization.branches.usage', { current: branches.length, limit: limits.maxBranches })}</span>
      ) : null}
    </div>
  );

  const dialog = addOpen ? (
    <NewBranchDialog
      client={organizationsClient}
      organizationId={organizationId}
      onClose={() => setAddOpen(false)}
      onCreated={branch => { onBranchCreated(branch); setAddOpen(false); }}
    />
  ) : null;

  if (branches.length === 0) {
    return (
      <div>
        {header}
        <EmptyState
          message={t('platform.organization.clubsTab.empty')}
          next={canAddBranch
            ? { label: t('platform.organization.branches.addFirst'), onClick: () => setAddOpen(true) }
            : { noPermission: t('state.empty.noPermission', { permission: t('platform.permission.organizations.create') }) }}
        />
        {dialog}
      </div>
    );
  }

  // Пока пульс в пути, карточка клуба без устройств, мест и последней связи читается как факт:
  // «клуб ни разу не вышел на связь». Скелетон на месте этих строк говорит правду — мы ещё не знаем.
  const pulsePending = pulseByBranch === null && !error;

  return (
    <div>
      {header}
      {error ? <PartialFailure title={t('platform.organization.clubsTab.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} /> : null}
      <div className="pc-card-grid">
        {branches.map(branch => {
          const club = pulseByBranch?.get(branch.branchId);
          return (
            <Card key={branch.branchId}>
              <CardHeader>
                <CardTitle>{branch.name}</CardTitle>
                {club !== undefined ? (
                  <Badge variant={club.shiftOpen ? 'success' : 'outline'}>
                    {club.shiftOpen ? t('platform.organization.clubsTab.shiftOpen') : t('platform.organization.clubsTab.shiftClosed')}
                  </Badge>
                ) : null}
              </CardHeader>
              <CardContent>
                <div className="pc-kv"><span>{branch.city}</span><code>{branch.slug}</code></div>
                {club === undefined && pulsePending ? (
                  <div className="pc-kv"><Skeleton className="pc-skel-value" /></div>
                ) : null}
                {club !== undefined ? (
                  <>
                    <div className="pc-kv"><span>{t('platform.organization.clubsTab.devices')}</span><span className="pc-num">{club.devicesOnline}/{club.devicesTotal}</span></div>
                    <div className="pc-kv"><span>{t('platform.organization.clubsTab.seats')}</span><span className="pc-num">{club.seatsOccupied}/{club.seatsTotal}</span></div>
                    <div className="pc-kv"><span>{t('platform.organization.clubsTab.lastHeartbeat')}</span><span>{club.lastHeartbeatAtUtc !== null ? formatDate(club.lastHeartbeatAtUtc) : '—'}</span></div>
                    {club.alerts.length > 0 ? (
                      <ul>
                        {club.alerts.map((alert, index) => (
                          <li key={`${alert.kind}-${index}`}>
                            <Badge
                              variant={alert.level === 'critical' ? 'destructive' : alert.level === 'attention' ? 'secondary' : 'outline'}
                              title={alertDetailText(alert, t)}
                            >
                              {t(alertLabel(alert))}
                            </Badge>
                          </li>
                        ))}
                      </ul>
                    ) : null}
                  </>
                ) : null}
              </CardContent>
            </Card>
          );
        })}
      </div>
      {dialog}
    </div>
  );
}
