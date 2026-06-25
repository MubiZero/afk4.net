import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes } from 'lucide-react';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  mapCatalogToStock,
  stockStatus,
  marginPercent,
  stockValueMinorUnits,
  summarize,
  type StockItem,
} from './stockLevels';

type FilterMode = 'all' | 'low' | 'out';

export function StockLevelsWorkspace({
  backend,
  currencyCode,
  session,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();

  const canView = hasPermission(session, permissionNames.viewInventory);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [items, setItems] = useState<StockItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [filter, setFilter] = useState<FilterMode>('all');
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (!canView) { setLoading(false); return; }
    if (clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((catalog) => {
        if (!alive) return;
        setItems(mapCatalogToStock(catalog));
      })
      .catch((error) => {
        if (alive) setLoadError(projectOperatorError(error, t).detail);
      })
      .finally(() => {
        if (alive) setLoading(false);
      });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canView]);

  if (!canView) {
    return (
      <section className="cash-stock-levels">
        <p className="workspace-error">{t('op.stock.levels.noPermission')}</p>
      </section>
    );
  }

  if (loading) {
    return (
      <div className="stock-layout">
        <section className="cash-stock-levels">
          <p className="workspace-loading">{t('op.stock.levels.loading')}</p>
        </section>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="stock-layout">
        <section className="cash-stock-levels">
          <p className="workspace-error" role="alert">{loadError}</p>
        </section>
      </div>
    );
  }

  const filtered = items.filter((item) => {
    if (filter === 'low') { const s = stockStatus(item); return s === 'low' || s === 'out'; }
    if (filter === 'out') return stockStatus(item) === 'out';
    return true;
  }).filter((item) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return item.name.toLowerCase().includes(q) || item.sku.toLowerCase().includes(q);
  });

  const summary = summarize(items);
  const orderItems = items.filter((i) => stockStatus(i) !== 'ok');


  return (
    <div className="stock-layout">
      {/* ── Список ── */}
      <section className="cash-stock-levels">
        <div className="levels-head">
          <h2>{t('op.stock.levels.title')}</h2>
          <div className="seg">
            <button
              type="button"
              className={filter === 'all' ? 'on' : ''}
              aria-pressed={filter === 'all'}
              onClick={() => setFilter('all')}
            >
              {t('op.stock.filter.all')} · {items.length}
            </button>
            <button
              type="button"
              className={filter === 'low' ? 'on' : ''}
              aria-pressed={filter === 'low'}
              onClick={() => setFilter('low')}
            >
              {t('op.stock.filter.low')} · {summary.lowCount + summary.outCount}
            </button>
            <button
              type="button"
              className={filter === 'out' ? 'on' : ''}
              aria-pressed={filter === 'out'}
              onClick={() => setFilter('out')}
            >
              {t('op.stock.filter.out')} · {summary.outCount}
            </button>
          </div>
          <div className="panel-search">
            <input
              type="search"
              placeholder={t('op.stock.levels.search')}
              value={search}
              onChange={(e) => setSearch(e.currentTarget.value)}
              aria-label={t('op.stock.levels.search')}
            />
          </div>
        </div>

        {/* Заголовки колонок */}
        <div className="cash-stock-cols srow" aria-hidden="true">
          <span />
          <span>{t('op.stock.col.item')}</span>
          <div className="metrics">
            <span>{t('op.stock.col.qty')}</span>
            <span>{t('op.stock.col.threshold')}</span>
            <span>{t('op.stock.col.cost')}</span>
            <span>{t('op.stock.col.price')}</span>
            <span>{t('op.stock.col.margin')}</span>
            <span>{t('op.stock.col.value')}</span>
            <span>{t('op.stock.col.actions')}</span>
          </div>
        </div>

        {items.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.stock.levels.empty')}</p>
        ) : filtered.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.stock.levels.emptyFiltered')}</p>
        ) : (
          <ul className="cash-stock-list">
            {filtered.map((item) => {
              const status = stockStatus(item);
              const margin = marginPercent(item.priceMinorUnits, item.avgCostMinorUnits);
              const stockVal = stockValueMinorUnits(item);
              return (
                <li key={item.productId} className={`cash-stock-row srow${status !== 'ok' ? ` ${status}` : ''}`}>
                  <Boxes size={15} aria-hidden="true" />
                  <div className="cell-name">
                    <strong>{item.name}</strong>
                    <em>
                      {item.sku}
                      {item.category && <span className="cat">{item.category}</span>}
                    </em>
                  </div>
                  <div className="metrics">
                    {/* Остаток */}
                    <div className={`qty${status === 'low' ? ' low' : status === 'out' ? ' out' : ''}`}>
                      {item.stockOnHand}
                      <span className="u"> {t('op.stock.col.unit')}</span>
                      {status !== 'ok' && (
                        <span className={`stock-status-tag ${status}`}>
                          {t(status === 'low' ? 'op.stock.status.low' : 'op.stock.status.out')}
                        </span>
                      )}
                    </div>
                    {/* Порог */}
                    <div className="thr">{item.reorderThreshold || '—'}</div>
                    {/* Себест */}
                    <div className="money">
                      {item.avgCostMinorUnits > 0 ? formatMinorUnits(item.avgCostMinorUnits, currencyCode) : <span className="dim">—</span>}
                    </div>
                    {/* Цена */}
                    <div className="money">
                      {item.priceMinorUnits > 0 ? formatMinorUnits(item.priceMinorUnits, currencyCode) : <span className="dim">—</span>}
                    </div>
                    {/* Маржа */}
                    <div className="marg">
                      {margin !== null ? `${margin}%` : <span className="dim">—</span>}
                    </div>
                    {/* Стоимость склада */}
                    <div className={`valm${stockVal <= 0 ? ' dim' : ''}`}>
                      {stockVal > 0 ? formatMinorUnits(stockVal, currencyCode) : '—'}
                    </div>
                    {/* Действия (S0 — заглушки) */}
                    <div className="rowact">
                      <button type="button" className="iact" disabled title={t('op.stock.summary.orderBtnSoon')} aria-label={t('op.stock.action.receive')} aria-disabled="true">＋</button>
                      <button type="button" className="iact minus" disabled title={t('op.stock.summary.orderBtnSoon')} aria-label={t('op.stock.action.writeOff')} aria-disabled="true">−</button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      {/* ── Сводка ── */}
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.summary.totalValue')}</h3>
          <div className="ctx-big">{formatMinorUnits(summary.totalValueMinorUnits, currencyCode)}</div>
          <div className="ctx-sub">
            {t('op.stock.summary.totalSub', { count: items.reduce((acc, i) => acc + Math.max(i.stockOnHand, 0), 0) })}
          </div>
          <div className="mv">
            <span>{t('op.stock.summary.lowCount')}</span>
            <b className="warning-text">{summary.lowCount}</b>
          </div>
          <div className="mv">
            <span>{t('op.stock.summary.outCount')}</span>
            <b className="danger-text">{summary.outCount}</b>
          </div>
        </div>

        {orderItems.length > 0 && (
          <div className="ctx-card">
            <h3 className="ctx-title">
              {t('op.stock.summary.orderTitle')}
              {' '}
              <span className="warning-text">{orderItems.length}</span>
            </h3>
            {orderItems.map((item) => {
              const s = stockStatus(item);
              return (
                <div key={item.productId} className="order-item" title={item.name}>
                  <strong className="order-item-name">{item.name}</strong>
                  <span className={`oq ${s}`}>{item.stockOnHand}/{item.reorderThreshold}</span>
                </div>
              );
            })}
            {/* S0: кнопка-заглушка, реальная приёмка в S1 */}
            <button type="button" className="ctx-btn" disabled aria-disabled="true">
              {t('op.stock.summary.orderBtnSoon')}
            </button>
          </div>
        )}
      </aside>
    </div>
  );
}
