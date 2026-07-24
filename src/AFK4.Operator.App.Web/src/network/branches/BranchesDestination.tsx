import { useMemo, useState } from 'react';
import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState, Money } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients, dashboardRangeQuery, toDateInputValue } from '../../operatorHelpers';
import { mapProfileToForm, buildUpdateBranchProfileRequest } from '../../settings/club/branchProfileRequest';
import type { ClubProfileForm } from '../../settings/club/ClubProfileFields';
import type { OperatorBackendContext } from '../../operatorTypes';
import { useBranchRollup, type RollupClient } from './useBranchRollup';
import { RenameBranchModal } from './RenameBranchModal';

interface RenameTarget { branchId: string; form: ClubProfileForm; }

// Свод по сети — Owner-эксклюзивный экран (гейт branches.view, см. networkNav.ts). Каждая
// карточка = today-KPI одного филиала (та же формула диапазона, что и рейл-KPI шелла —
// dashboardRangeQuery), плюс итоговая строка сложением по всем загруженным филиалам.
// «Открыть филиал» здесь намеренно не реализовано: переключение активного филиала живёт в
// useActiveBranch (App-level, localStorage-реактивный), а переход-на-Карту требует setWorkspace,
// которого этот раздел не видит. Прокидывать onOpenBranch через NetworkWorkspace←WorkspaceRouter←
// App ради одной кнопки — раздувание контракта каркаса; follow-up, не заглушка.
export function BranchesDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatNumber } = useI18n();

  const client = useMemo<RollupClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const today = toDateInputValue(new Date());
    return {
      getOwnerBranches: () => clients.orgBranches.getOwnerBranches(),
      getBranchProfile: (id) => clients.settings.getBranchProfile(id),
      getBranchSummary: (id) => clients.dashboard.getSummary(id, dashboardRangeQuery(today, today))
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBranchRollup(
    client ?? { getOwnerBranches: async () => [], getBranchProfile: async () => ({}), getBranchSummary: async () => ({}) },
    t('op.network.branches.unnamed')
  );
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);

  const screenState = backend === null ? 'loading' : state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.branches')}
      subtitle={t('op.network.dest.branches.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          <div className="network-branches-totals">
            <Totals label={t('op.network.branches.totals.branches')} value={formatNumber(state.data.totals.branches)} />
            <Totals label={t('op.network.branches.kpi.devices')} value={`${formatNumber(state.data.totals.devicesOnline.online)} / ${formatNumber(state.data.totals.devicesOnline.total)}`} />
            <Totals label={t('op.network.branches.kpi.sessions')} value={formatNumber(state.data.totals.activeSessions)} />
            <Totals label={t('op.network.branches.kpi.revenue')} value={<Money minorUnits={state.data.totals.revenue.minorUnits} currencyCode={state.data.totals.revenue.currencyCode} />} />
            <Totals label={t('op.network.branches.kpi.attention')} value={formatNumber(state.data.totals.attention)} />
          </div>

          {state.data.rows.length === 0 ? (
            <EmptyState title={t('op.network.branches.empty')} />
          ) : (
            <div className="network-branches-grid">
              {state.data.rows.map((row) => (
                <section key={row.branchId} className="management-panel network-branch-card">
                  <header>
                    <h3>{row.name}</h3>
                    <span className="network-branch-city">{row.city}</span>
                  </header>
                  {row.kpis === null ? (
                    <p className="network-branch-error">{t('op.network.branches.card.error')}</p>
                  ) : (
                    <dl className="network-branch-kpis">
                      <Stat label={t('op.network.branches.kpi.devices')} value={`${formatNumber(row.kpis.devicesOnline.online)} / ${formatNumber(row.kpis.devicesOnline.total)}`} />
                      <Stat label={t('op.network.branches.kpi.sessions')} value={formatNumber(row.kpis.activeSessions)} />
                      <Stat label={t('op.network.branches.kpi.revenue')} value={<Money minorUnits={row.kpis.revenue.minorUnits} currencyCode={row.kpis.revenue.currencyCode} />} />
                      <Stat label={t('op.network.branches.kpi.attention')} value={formatNumber(row.kpis.attention)} />
                    </dl>
                  )}
                  <div className="network-branch-actions">
                    <button
                      type="button"
                      className="ui-btn"
                      // updateBranchProfile is a full-record PATCH — renaming needs the branch's
                      // complete profile (contacts/hours/etc.), not just name+city. If that
                      // branch's profile fetch failed, honestly disable rename instead of
                      // submitting a payload built from guessed defaults.
                      disabled={state.profiles[row.branchId] == null}
                      onClick={() => {
                        const profile = state.profiles[row.branchId];
                        if (profile == null) return;
                        setRenameTarget({ branchId: row.branchId, form: mapProfileToForm(profile) });
                      }}
                    >
                      {t('op.network.branches.rename')}
                    </button>
                  </div>
                </section>
              ))}
            </div>
          )}

          <div className="network-branches-add">
            <button type="button" className="ui-btn" disabled>{t('op.network.branches.add')}</button>
            <p className="network-branches-add-note">{t('op.network.branches.add.unavailable')}</p>
          </div>

          {renameTarget !== null && backend !== null && (
            <RenameBranchModal
              organizationId={backend.session.organizationId}
              initialName={renameTarget.form.name}
              initialCity={renameTarget.form.city}
              onClose={() => setRenameTarget(null)}
              onSave={async (request) => {
                const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
                const mergedForm: ClubProfileForm = { ...renameTarget.form, name: request.name, city: request.city };
                const fullRequest = buildUpdateBranchProfileRequest(backend.session.organizationId, mergedForm);
                await clients.settings.updateBranchProfile(renameTarget.branchId, fullRequest);
                state.retry();
              }}
            />
          )}
        </>
      )}
    </ManagementScreen>
  );
}

function Totals({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="management-panel network-total">
      <span className="network-total-label">{label}</span>
      <span className="network-total-value">{value}</span>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="network-stat">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
