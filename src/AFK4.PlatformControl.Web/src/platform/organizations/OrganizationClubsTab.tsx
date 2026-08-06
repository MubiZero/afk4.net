import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { PartialFailure } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { alertDetailText, alertLabel } from '@/platform/clubs/pulseModel';
import type { PulseApi } from '@/api/platformClients/pulse';
import type { OrganizationBranch, PulseClub } from '@/api/types';

type Client = Pick<PulseApi, 'getPulse'>;

/**
 * The branch roster is structural truth (organization detail); the pulse only
 * adds a live overlay (devices/seats/shift) when a branch has reported in.
 * A pulse outage must not hide the roster itself.
 */
export function OrganizationClubsTab({ client, organizationId, branches }: { client: Client; organizationId: string; branches: OrganizationBranch[] }) {
  const { t, formatDate } = useI18n();
  const [tick, setTick] = useState(0);
  const [pulseByBranch, setPulseByBranch] = useState<Map<string, PulseClub> | null>(null);
  const [error, setError] = useState(false);

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

  if (branches.length === 0) return <p>{t('platform.organization.clubsTab.empty')}</p>;

  return (
    <div>
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
    </div>
  );
}
