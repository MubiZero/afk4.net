import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { Pencil } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { Money } from '../../operatorPrimitives';
import { SkeletonLine, SkeletonTable } from '../../LoadingSkeleton';
import { MgmtTable } from '../../management/kit/MgmtTable';
import type { BranchRollupRow } from './branchRollupModel';
import { projectOperatorError } from '../../apiErrors';
import { Num } from '@afk4/ui/react';
import { createAuthenticatedOperatorClients, dashboardRangeQuery, toDateInputValue } from '../../operatorHelpers';
import { mapProfileToForm, buildUpdateBranchProfileRequest } from '../../settings/club/branchProfileRequest';
import type { ClubProfileForm } from '../../settings/club/ClubProfileFields';
import type { OperatorBackendContext } from '../../operatorTypes';
import { useBranchRollup, type RollupClient } from './useBranchRollup';
import { RenameBranchModal } from './RenameBranchModal';

interface RenameTarget { branchId: string; form: ClubProfileForm; }

// Колонки списка — одни на список и его заглушку.
const BRANCHES_GRID = 'minmax(0, 1.6fr) minmax(0, 1fr) minmax(0, 0.8fr) minmax(0, 1fr) minmax(0, 1fr)';

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

  const state = useBranchRollup(client, t('op.network.branches.unnamed'));
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);

  const screenState = backend === null ? 'loading' : state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.branches')}
      contentWidth="full"
      state={screenState}
      skeleton={
        <>
          <section className="management-panel network-branches-figure" aria-hidden="true">
            <span className="ui-section-label">{t('op.network.branches.figure.label')}</span>
            <span className="network-branches-figure-value"><SkeletonLine width="6em" /></span>
          </section>
          <SkeletonTable gridTemplate={BRANCHES_GRID} rowActions rows={3} />
        </>
      }
      failure={state.status === 'error' ? projectOperatorError(state.error, t) : undefined}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          {/* Одна главная цифра — выручка сети за сегодня, а остальное — списком по филиалам. Было
              пять плиток итогов, и каждая повторялась ещё раз в карточке каждого филиала. */}
          <section className="management-panel network-branches-figure">
            <span className="ui-section-label">{t('op.network.branches.figure.label')}</span>
            <Money className="network-branches-figure-value" minorUnits={state.data.totals.revenue.minorUnits} currencyCode={state.data.totals.revenue.currencyCode} />
            <span className="network-branches-figure-hint">
              {t('op.network.branches.figure.hint', {
                branches: state.data.totals.branches,
                online: state.data.totals.devicesOnline.online,
                total: state.data.totals.devicesOnline.total
              })}
            </span>
          </section>

          <MgmtTable<BranchRollupRow>
            columns={[
              {
                key: 'name',
                header: t('op.network.branches.col.branch'),
                render: (row) => (
                  <span className="network-branch-name">
                    <strong>{row.name}</strong>
                    {row.city && <span>{row.city}</span>}
                  </span>
                )
              },
              {
                key: 'devices',
                header: t('op.network.branches.kpi.devices'),
                align: 'end',
                render: (row) => row.kpis === null ? '—' : <Num>{t('op.network.branches.devicesOf', { online: formatNumber(row.kpis.devicesOnline.online), total: formatNumber(row.kpis.devicesOnline.total) })}</Num>
              },
              {
                key: 'sessions',
                header: t('op.network.branches.kpi.sessions'),
                align: 'end',
                render: (row) => row.kpis === null ? '—' : <Num>{formatNumber(row.kpis.activeSessions)}</Num>
              },
              {
                key: 'revenue',
                header: t('op.network.branches.kpi.revenue'),
                align: 'end',
                render: (row) => row.kpis === null
                  ? <span className="network-branch-error">{t('op.network.branches.card.error')}</span>
                  : <Money minorUnits={row.kpis.revenue.minorUnits} currencyCode={row.kpis.revenue.currencyCode} />
              },
              {
                key: 'attention',
                header: t('op.network.branches.kpi.attention'),
                align: 'end',
                render: (row) => row.kpis === null ? '—' : <Num>{formatNumber(row.kpis.attention)}</Num>
              }
            ]}
            rows={state.data.rows}
            rowKey={(row) => row.branchId}
            gridTemplate={BRANCHES_GRID}
            rowActions={(row) => [{
              id: 'rename',
              label: t('op.network.branches.rename'),
              icon: <Pencil size={14} aria-hidden="true" />,
              // updateBranchProfile is a full-record PATCH — renaming needs the branch's complete
              // profile (contacts/hours/etc.), not just name+city. If that branch's profile fetch
              // failed, honestly disable rename instead of submitting guessed defaults.
              disabled: state.profiles[row.branchId] == null,
              onSelect: () => {
                const profile = state.profiles[row.branchId];
                if (profile == null) return;
                setRenameTarget({ branchId: row.branchId, form: mapProfileToForm(profile) });
              }
            }]}
            empty={{ title: t('op.network.branches.empty'), next: { kind: 'elsewhere', hint: t('op.network.branches.add.viaPlatform') } }}
          />

          {/* Кнопки здесь нет намеренно, и надпись объясняет почему, а не отговаривается словом
              «пока». Новый филиал меняет лимит ПК в тарифе и счёт клуба — это разговор с
              платформой, а не действие стойки, и вечно выключенная кнопка обещала обратное. */}
          {state.data.rows.length > 0 && <p className="network-branches-add-note">{t('op.network.branches.add.viaPlatform')}</p>}

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
