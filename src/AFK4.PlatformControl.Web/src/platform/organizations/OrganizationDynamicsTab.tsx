import { useState } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select } from '@/components/ui/select';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { BranchDynamicsApi } from '@/api/platformClients/branchDynamics';
import type { BranchDynamics, OrganizationBranch } from '@/api/types';
import { summarizeAgentDays, toDynamicsSeries } from './dynamicsModel';
import { useBranchDynamics } from './useBranchDynamics';

type Client = Pick<BranchDynamicsApi, 'getBranchDynamics'>;

export function OrganizationDynamicsTab({ client, organizationId, branches }: {
  client: Client;
  organizationId: string;
  branches: OrganizationBranch[];
}) {
  const i18n = useI18n();
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const state = useBranchDynamics(client, organizationId, branchId);

  if (branches.length === 0) return <EmptyState message={i18n.t('platform.dynamics.empty')} />;

  return (
    <div className="pc-analytics">
      {branches.length > 1 ? (
        <label className="ui-field">
          <span>{i18n.t('platform.dynamics.branch.label')}</span>
          <Select value={branchId} onChange={event => setBranchId(event.target.value)}>
            {branches.map(branch => (
              <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
            ))}
          </Select>
        </label>
      ) : null}

      {state.status === 'loading' ? <LoadingCards count={3} /> : null}
      {state.status === 'error' ? (
        <ErrorState message={i18n.t('platform.dynamics.error')} retryLabel={i18n.t('platform.dynamics.retry')} onRetry={state.retry} />
      ) : null}
      {state.status === 'ready' ? <DynamicsContent i18n={i18n} data={state.data} /> : null}
    </div>
  );
}

// Верхнеуровневая функция, а не компонент, вложенный в рендер OrganizationDynamicsTab: вложенное
// определение пересоздавало бы тип компонента на каждый рендер и размонтировало бы поддерево.
function DynamicsContent({ i18n, data }: { i18n: ReturnType<typeof useI18n>; data: BranchDynamics }) {
  const { t, formatCurrency, formatNumber } = i18n;

  if (data.days.length === 0) return <EmptyState message={t('platform.dynamics.empty')} />;

  const series = toDynamicsSeries(data.days);
  const agentSummary = summarizeAgentDays(data.days);

  return (
    <>
      <div className="pc-analytics-summary">
        <SummaryTile
          label={t('platform.dynamics.summary.revenue')}
          value={formatCurrency(minorToMajor(data.totalRevenue.minorUnits), data.totalRevenue.currencyCode)}
        />
        <SummaryTile
          label={t('platform.dynamics.summary.sessions')}
          value={formatNumber(data.totalSessionCount)}
        />
        <SummaryTile value={t('platform.dynamics.summary.daysWithoutAgent', { count: data.daysWithoutAgent })} />
      </div>

      {data.daysWithUnknownAgent > 0 || data.missingDayCount > 0 ? (
        <p className="pc-analytics-empty">
          {data.daysWithUnknownAgent > 0 ? t('platform.dynamics.summary.daysUnknown', { count: data.daysWithUnknownAgent }) : null}
          {data.daysWithUnknownAgent > 0 && data.missingDayCount > 0 ? ' · ' : null}
          {data.missingDayCount > 0 ? t('platform.dynamics.summary.missingDays', { count: data.missingDayCount }) : null}
        </p>
      ) : null}

      {/* Явные три величины связи рядом: не выходил на связь (agentSummary.dead) и нет данных
          (agentSummary.unknown) — разные факты, которые нельзя схлопывать в один «плохой» бакет. */}
      <div className="pc-kv"><span>{t('platform.dynamics.agent.alive')}</span><span className="pc-num">{formatNumber(agentSummary.alive)}</span></div>
      <div className="pc-kv"><span>{t('platform.dynamics.agent.dead')}</span><span className="pc-num">{formatNumber(agentSummary.dead)}</span></div>
      <div className="pc-kv"><span>{t('platform.dynamics.agent.unknown')}</span><span className="pc-num">{formatNumber(agentSummary.unknown)}</span></div>

      <Card>
        <CardHeader>
          <CardTitle>{t('platform.dynamics.chart.revenue')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="pc-analytics-chart">
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={series}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border-soft)" />
                <XAxis dataKey="date" stroke="var(--text-tertiary)" fontSize={12} />
                <YAxis stroke="var(--text-tertiary)" fontSize={12} />
                <Tooltip
                  formatter={value => formatCurrency(Number(value), data.totalRevenue.currencyCode)}
                  contentStyle={{ background: 'var(--surface-elevated)', border: '1px solid var(--border-default)', borderRadius: 'var(--radius-sm)' }}
                />
                <Line type="monotone" dataKey="revenue" stroke="var(--accent)" name="revenue" />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('platform.dynamics.chart.sessions')}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="pc-analytics-chart">
            <ResponsiveContainer width="100%" height={220}>
              <LineChart data={series}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border-soft)" />
                <XAxis dataKey="date" stroke="var(--text-tertiary)" fontSize={12} />
                <YAxis stroke="var(--text-tertiary)" fontSize={12} allowDecimals={false} />
                <Tooltip contentStyle={{ background: 'var(--surface-elevated)', border: '1px solid var(--border-default)', borderRadius: 'var(--radius-sm)' }} />
                <Line type="monotone" dataKey="sessions" stroke="var(--success)" name="sessions" />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </CardContent>
      </Card>

      <p className="pc-analytics-footnote">{t('platform.dynamics.footnote')}</p>
    </>
  );
}

function SummaryTile({ label, value }: { label?: string; value: string }) {
  return (
    <div className="pc-analytics-tile">
      {label !== undefined ? <span className="pc-analytics-tile-label">{label}</span> : null}
      <span className="pc-analytics-tile-value ui-money">{value}</span>
    </div>
  );
}
