import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardToolbar } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Inspector, Num } from '@afk4/ui/react';
import { EmptyState, PartialFailure } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { ALERT_BADGE, alertDetailText, alertLabel } from '@/platform/clubs/pulseModel';
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

  // Сколько филиалов из разрешённых — тихой строкой слева, кнопка — справа, одним рядом: раньше
  // счётчик стоял браузерным шрифтом вплотную к кнопке.
  const header = (
    <CardToolbar hint={limits.maxBranches !== null ? t('platform.organization.branches.usage', { current: branches.length, limit: limits.maxBranches }) : undefined}>
      {canAddBranch ? <Button variant="outline" size="sm" onClick={() => setAddOpen(true)}>{t('platform.organization.branches.add')}</Button> : null}
    </CardToolbar>
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
                {club !== undefined && !club.shiftOpen ? <Badge variant="outline">{t('platform.organization.clubsTab.shiftClosed')}</Badge> : null}
              </CardHeader>
              <CardContent>
                {/* Короткий адрес филиала (slug) — служебный ключ ссылок; по нему филиал не узнают.
                    Факты — «подпись — значение» кита (.ui-facts), тем же видом, что в паспорте. */}
                <p className="mgmt-drawer-hint">{branch.city}</p>
                {club === undefined && pulsePending ? <Skeleton className="pc-skel-value" /> : null}
                {club !== undefined ? (
                  <>
                    <Inspector.Facts items={[
                      { label: t('platform.organization.clubsTab.devices'), value: <Num>{t('platform.organization.clubsTab.ofTotal', { count: club.devicesOnline, total: club.devicesTotal })}</Num> },
                      { label: t('platform.organization.clubsTab.seats'), value: <Num>{t('platform.organization.clubsTab.ofTotal', { count: club.seatsOccupied, total: club.seatsTotal })}</Num> },
                      { label: t('platform.organization.clubsTab.lastHeartbeat'), value: <Num>{club.lastHeartbeatAtUtc !== null ? formatDate(club.lastHeartbeatAtUtc) : '—'}</Num> }
                    ]} />
                    {club.alerts.length > 0 ? (
                      <ul className="pulse-alerts">
                        {club.alerts.map((alert, index) => (
                          <li key={`${alert.kind}-${index}`} className="pulse-alert">
                            <Badge variant={ALERT_BADGE[alert.level]}>{t(alertLabel(alert))}</Badge>
                            {alertDetailText(alert, t) !== undefined ? <span className="pulse-alert-detail">{alertDetailText(alert, t)}</span> : null}
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
