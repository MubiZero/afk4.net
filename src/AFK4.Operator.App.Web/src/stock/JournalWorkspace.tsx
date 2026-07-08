import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { ArrowDownToLine, ClipboardList } from 'lucide-react';
import { useDeferredFlag } from '../useDeferredFlag';
import { EmptyState, Money } from '../operatorPrimitives';
import { StockSkeleton } from './StockSkeleton';
import { StockHero } from './StockHero';
import { createAuthenticatedOperatorClients, stockMovementTypeLabel } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { PosProductDto, StockMovementDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  mapMovementsToRows, filterByType, filterByPeriod, groupByDay, summarize, buildCsv, movementStatusTone,
  type JournalTypeFilter, type JournalPeriod, type MovementType,
} from './journalModel';

const TYPE_FILTERS: JournalTypeFilter[] = ['all', 'purchase', 'sale', 'refund', 'adjustment'];
const PERIODS: JournalPeriod[] = ['today', 'week', 'all'];
const PERIOD_LABEL_KEYS: Record<JournalPeriod, MessageKey> = {
  today: 'op.stock.journal.period.today',
  week: 'op.stock.journal.period.week',
  all: 'op.stock.journal.period.all',
};
const MOVEMENT_LIMIT = 200;

export function JournalWorkspace({
  backend,
  currencyCode,
  session,
  refreshNonce = 0,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  refreshNonce?: number;
}) {
  const { t, locale } = useI18n();
  const canView = hasAnyPermission(session, [permissionNames.viewInventory, permissionNames.manageInventoryStock]);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [movements, setMovements] = useState<StockMovementDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [typeFilter, setTypeFilter] = useState<JournalTypeFilter>('all');
  // Дефолт 'all' (последние ≤200) — всегда показывает свежую активность, без пустоты тихим утром
  // и без завязки тестов на текущую дату. Сегодня/7 дней — опциональное сужение.
  const [period, setPeriod] = useState<JournalPeriod>('all');
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (!canView || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    Promise.all([
      clients.inventory.getStockMovements(backend.branchId, { limit: MOVEMENT_LIMIT }),
      clients.pos.getCatalog(backend.branchId),
    ])
      .then(([loadedMovements, loadedCatalog]) => {
        if (!alive) return;
        setMovements(Array.isArray(loadedMovements) ? loadedMovements as StockMovementDto[] : []);
        setCatalog(Array.isArray(loadedCatalog) ? loadedCatalog as PosProductDto[] : []);
      })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canView, refreshNonce]);

  const dateTimeFmt = useMemo(() => new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }), [locale]);
  const dayFmt = useMemo(() => new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }), [locale]);

  const showSkeleton = useDeferredFlag(loading);

  if (!canView) {
    return <section className="stock-journal"><p className="workspace-error">{t('op.stock.journal.noPermission')}</p></section>;
  }
  if (loading && movements.length === 0) {
    return showSkeleton
      ? <StockSkeleton sectionClass="stock-journal" label={t('op.stock.journal.loading')} />
      : <div className="stock-layout" />;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-journal"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const allRows = mapMovementsToRows(movements, catalog);
  const nowMs = Date.now();
  const periodRows = filterByPeriod(allRows, period, nowMs);
  const query = search.trim().toLowerCase();
  const rows = filterByType(periodRows, typeFilter).filter((row) =>
    !query || row.name.toLowerCase().includes(query) || row.sku.toLowerCase().includes(query));
  const groups = groupByDay(rows);
  const summary = summarize(periodRows);

  const dayLabel = (dayKey: string): string => {
    const todayKey = new Date(nowMs).toISOString().slice(0, 10);
    const yesterdayKey = new Date(nowMs - 86_400_000).toISOString().slice(0, 10);
    if (dayKey === todayKey) return t('op.stock.journal.today');
    if (dayKey === yesterdayKey) return t('op.stock.journal.yesterday');
    return dayFmt.format(new Date(`${dayKey}T00:00:00Z`));
  };

  const exportCsv = () => {
    const csv = buildCsv(rows, {
      headers: [
        t('op.stock.journal.csv.dateTime'), t('op.stock.journal.csv.type'), t('op.stock.journal.csv.product'),
        t('op.stock.journal.csv.sku'), t('op.stock.journal.csv.qty'), t('op.stock.journal.csv.unitCost'),
        t('op.stock.journal.csv.sum'), t('op.stock.journal.csv.reason'), t('op.stock.journal.csv.who'),
      ],
      typeLabel: (type) => stockMovementTypeLabel(type, t),
      formatMoney: (minor) => (minor / 100).toFixed(2),
      formatDateTime: (iso) => new Date(iso).toISOString().replace('T', ' ').slice(0, 16),
    });
    const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `stock-journal-${new Date(nowMs).toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const capReached = movements.length >= MOVEMENT_LIMIT;

  return (
    <div className="stock-layout">
      <section className="stock-journal">
        <div className="ledger-head">
          <h2>{t('op.stock.journal.head')}</h2>
          <div className="seg">
            {TYPE_FILTERS.map((filter) => (
              <button
                key={filter}
                type="button"
                className={`ui-chip ui-chip--filter${typeFilter === filter ? ' is-active' : ''}`}
                aria-pressed={typeFilter === filter}
                onClick={() => setTypeFilter(filter)}
              >
                {filter === 'all' ? t('op.stock.journal.filter.all') : stockMovementTypeLabel(filter, t)}
              </button>
            ))}
          </div>
          <div className="panel-search">
            <input
              type="search"
              placeholder={t('op.stock.journal.search')}
              value={search}
              aria-label={t('op.stock.journal.search')}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
          </div>
          <button type="button" className="cash-command-btn journal-export" onClick={exportCsv} disabled={rows.length === 0}>
            <ArrowDownToLine size={14} aria-hidden="true" />
            {t('op.stock.journal.export')}
          </button>
        </div>

        {capReached && <p className="journal-cap">{t('op.stock.journal.capNote', { count: MOVEMENT_LIMIT })}</p>}

        {allRows.length === 0 ? (
          <EmptyState icon={<ClipboardList size={28} aria-hidden="true" />} title={t('op.stock.journal.empty')} />
        ) : rows.length === 0 ? (
          <EmptyState icon={<ClipboardList size={28} aria-hidden="true" />} title={t('op.stock.journal.emptyFiltered')} />
        ) : (
          <div className="jledger" aria-label={t('op.stock.journal.head')}>
            {groups.map((group) => (
              <div key={group.dayKey}>
                <div className="daygroup">{dayLabel(group.dayKey)}</div>
                <ul className="jlist ui-ledger-list">
                  {group.rows.map((row) => (
                    <li key={row.id} className="ui-ledger-row stock-ledger-row">
                      <span className="ui-ledger-time">{dateTimeFmt.format(new Date(row.createdAtUtc))}</span>
                      <div className="ui-ledger-body">
                        <span className="ui-ledger-title">
                          <span className={`ui-chip ui-chip--status ${movementStatusTone(row.type as MovementType, row.quantityDelta)}`}>
                            {stockMovementTypeLabel(row.type, t)}
                          </span>
                          {row.name}
                        </span>
                        <span className="ui-ledger-detail">
                          {row.sku}{row.reason ? ` · ${row.reason}` : ''}{row.who ? ` · ${row.who}` : ''}
                        </span>
                      </div>
                      <span className="ui-ledger-aside">
                        <span className="ui-money">{row.quantityDelta > 0 ? '+' : ''}{row.quantityDelta} {t('op.stock.journal.unit')}</span>
                        {row.sumMinorUnits > 0
                          ? <Money minorUnits={row.quantityDelta < 0 ? -row.sumMinorUnits : row.sumMinorUnits} currencyCode={currencyCode} signed />
                          : <span className="ui-money ui-money--muted">—</span>}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        )}
      </section>

      <aside className="stock-summary">
        <section className="stock-section">
          <h3 className="ctx-title">{t('op.stock.journal.period.title')}</h3>
          <div className="period">
            {PERIODS.map((value) => (
              <button
                key={value}
                type="button"
                className={`ui-chip ui-chip--filter${period === value ? ' is-active' : ''}`}
                aria-pressed={period === value}
                onClick={() => setPeriod(value)}
              >
                {t(PERIOD_LABEL_KEYS[value])}
              </button>
            ))}
          </div>
        </section>

        <StockHero
          label={t('op.stock.journal.summary.net')}
          value={summary.netQty > 0 ? `+${summary.netQty}` : String(summary.netQty)}
          tone={summary.netQty > 0 ? 'ok' : summary.netQty < 0 ? 'warning' : 'muted'}
        />

        <section className="stock-section">
          <div className="mv"><span>{t('op.stock.journal.summary.inbound')}</span><b className="in">+{summary.inboundQty} · <Money minorUnits={summary.inboundSumMinor} currencyCode={currencyCode} /></b></div>
          <div className="mv"><span>{t('op.stock.journal.summary.sold')}</span><b>−{summary.soldQty}</b></div>
          <div className="mv"><span>{t('op.stock.journal.summary.writtenOff')}</span><b className="wn">−{summary.writtenOffQty} · <Money minorUnits={summary.writtenOffSumMinor} currencyCode={currencyCode} /></b></div>
        </section>
      </aside>
    </div>
  );
}
