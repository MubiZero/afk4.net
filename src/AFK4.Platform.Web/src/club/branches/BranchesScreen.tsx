import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { LoadingCards, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useBranchRollup } from './useBranchRollup';
import { RenameBranchDialog } from './RenameBranchDialog';

type Client = Pick<ClubApiClient, 'getDashboardSummary' | 'getBranchProfile' | 'updateBranchProfile'>;

interface RenameTarget { branchId: string; name: string; city: string; }

export function BranchesScreen({ client, branchIds, organizationId, onOpenBranch }: {
  client: Client;
  branchIds: readonly string[];
  organizationId: string;
  onOpenBranch: (branchId: string) => void;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useBranchRollup(client, branchIds, t('branches.unnamed'));
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);

  if (state.status === 'loading') return <LoadingCards />;

  const { rows, totals } = state.data;

  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
        <Kpi label={t('branches.totals.branches')} value={formatNumber(totals.branches)} />
        <Kpi label={t('overview.kpi.devicesOnline')} value={`${formatNumber(totals.devicesOnline.online)} / ${formatNumber(totals.devicesOnline.total)}`} />
        <Kpi label={t('overview.kpi.activeSessions')} value={formatNumber(totals.activeSessions)} />
        <Kpi label={t('overview.kpi.revenueToday')} value={formatCurrency(totals.revenue.amount, totals.revenue.currencyCode)} />
        <Kpi label={t('overview.kpi.attention')} value={formatNumber(totals.attention)} />
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('branches.empty')} />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {rows.map(row => (
            <Card key={row.branchId}>
              <CardHeader>
                <CardTitle>{row.name}</CardTitle>
                <div className="text-xs text-muted-foreground">{row.city}</div>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                {row.kpis === null ? (
                  <p className="text-sm text-muted-foreground">{t('branches.card.error')}</p>
                ) : (
                  <dl className="grid grid-cols-2 gap-2 text-sm">
                    <Stat label={t('overview.kpi.devicesOnline')} value={`${formatNumber(row.kpis.devicesOnline.online)} / ${formatNumber(row.kpis.devicesOnline.total)}`} />
                    <Stat label={t('overview.kpi.activeSessions')} value={formatNumber(row.kpis.activeSessions)} />
                    <Stat label={t('overview.kpi.revenueToday')} value={formatCurrency(row.kpis.revenueToday.amount, row.kpis.revenueToday.currencyCode)} />
                    <Stat label={t('overview.kpi.attention')} value={formatNumber(row.kpis.attention)} />
                  </dl>
                )}
                <div className="flex gap-2">
                  <Button onClick={() => onOpenBranch(row.branchId)}>{t('branches.open')}</Button>
                  <Button variant="outline" onClick={() => setRenameTarget({ branchId: row.branchId, name: row.name, city: row.city })}>
                    {t('branches.rename')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <div className="flex flex-col items-start gap-2 border-t border-border pt-4">
        <Button disabled>{t('branches.add')}</Button>
        <p className="text-xs text-muted-foreground">{t('branches.add.unavailable')}</p>
      </div>

      {renameTarget !== null && (
        <RenameBranchDialog
          key={renameTarget.branchId}
          open
          branchId={renameTarget.branchId}
          organizationId={organizationId}
          initialName={renameTarget.name}
          initialCity={renameTarget.city}
          client={client}
          onOpenChange={(o) => { if (!o) setRenameTarget(null); }}
          onDone={() => state.retry()}
        />
      )}
    </div>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
    </CardContent></Card>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  );
}
