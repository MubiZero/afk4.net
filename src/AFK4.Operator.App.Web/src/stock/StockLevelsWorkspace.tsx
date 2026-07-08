import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { AlertTriangle, Boxes, Minus, Plus } from 'lucide-react';
import { useDeferredFlag } from '../useDeferredFlag';
import { EmptyState, Money } from '../operatorPrimitives';
import { StockSkeleton } from './StockSkeleton';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  mapCatalogToStock,
  stockStatus,
  stockValueMinorUnits,
  summarize,
  type StockItem,
} from './stockLevels';
import { StockHero } from './StockHero';
import { WriteOffDialog } from './WriteOffDialog';

type FilterMode = 'all' | 'low' | 'out';

export function StockLevelsWorkspace({
  backend,
  currencyCode,
  session,
  onReceive,
  onStockChanged,
  refreshNonce = 0,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  onReceive?: (productId?: string) => void;
  onStockChanged?: () => void;
  refreshNonce?: number;
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
  const [writeOffItem, setWriteOffItem] = useState<StockItem | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

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
  }, [clients, backend?.branchId, canView, reloadNonce, refreshNonce]);

  const showSkeleton = useDeferredFlag(loading);

  if (!canView) {
    return (
      <section className="cash-stock-levels">
        <p className="workspace-error">{t('op.stock.levels.noPermission')}</p>
      </section>
    );
  }

  if (loading && items.length === 0) {
    return showSkeleton
      ? <StockSkeleton sectionClass="cash-stock-levels" label={t('op.stock.levels.loading')} />
      : <div className="stock-layout" />;
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
              className={`ui-chip ui-chip--filter${filter === 'all' ? ' is-active' : ''}`}
              aria-pressed={filter === 'all'}
              onClick={() => setFilter('all')}
            >
              {t('op.stock.filter.all')} · {items.length}
            </button>
            <button
              type="button"
              className={`ui-chip ui-chip--filter${filter === 'low' ? ' is-active' : ''}`}
              aria-pressed={filter === 'low'}
              onClick={() => setFilter('low')}
            >
              {t('op.stock.filter.low')} · {summary.lowCount + summary.outCount}
            </button>
            <button
              type="button"
              className={`ui-chip ui-chip--filter${filter === 'out' ? ' is-active' : ''}`}
              aria-pressed={filter === 'out'}
              onClick={() => setFilter('out')}
            >
              {t('op.stock.filter.out')} · {summary.outCount}
            </button>
          </div>
          <div className="ui-field panel-search">
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
            <span>{t('op.stock.col.cost')}</span>
            <span>{t('op.stock.col.price')}</span>
            <span>{t('op.stock.col.value')}</span>
            <span>{t('op.stock.col.actions')}</span>
          </div>
        </div>

        {items.length === 0 ? (
          <EmptyState
            icon={<Boxes size={28} aria-hidden="true" />}
            title={t('op.stock.levels.empty')}
            action={onReceive ? { label: t('op.stock.summary.orderBtn'), onClick: () => onReceive() } : undefined}
          />
        ) : filtered.length === 0 ? (
          <EmptyState icon={<Boxes size={28} aria-hidden="true" />} title={t('op.stock.levels.emptyFiltered')} />
        ) : (
          <ul className="cash-stock-list">
            {filtered.map((item) => {
              const status = stockStatus(item);
              const stockVal = stockValueMinorUnits(item);
              return (
                <li key={item.productId} className={`cash-stock-row srow${status !== 'ok' ? ` ${status}` : ''}`}>
                  {status === 'ok'
                    ? <Boxes size={15} aria-hidden="true" />
                    : <AlertTriangle size={15} aria-hidden="true" className={`row-status-ico ${status}`} />}
                  <div className="cell-name">
                    <strong>{item.name}</strong>
                    <em>
                      {item.sku}
                      {item.category && <span className="cat">{item.category}</span>}
                    </em>
                  </div>
                  <div className="metrics">
                    {/* Остаток: число + «шт» в строке, статус — подсказкой снизу */}
                    <div className={`qty${status === 'low' ? ' low' : status === 'out' ? ' out' : ''}`}>
                      <span className="qnum">
                        {item.stockOnHand}
                        <span className="u"> {t('op.stock.col.unit')}</span>
                      </span>
                      {status === 'low' && (
                        <span className="ui-chip ui-chip--status is-warning">
                          {t('op.stock.status.low')}
                        </span>
                      )}
                    </div>
                    {/* Себест */}
                    <div className="money">
                      {item.avgCostMinorUnits > 0
                        ? <Money minorUnits={item.avgCostMinorUnits} currencyCode={currencyCode} />
                        : <span className="ui-money ui-money--muted">—</span>}
                    </div>
                    {/* Цена */}
                    <div className="money">
                      {item.priceMinorUnits > 0
                        ? <Money minorUnits={item.priceMinorUnits} currencyCode={currencyCode} />
                        : <span className="ui-money ui-money--muted">—</span>}
                    </div>
                    {/* Стоимость склада */}
                    <div className="valm">
                      {stockVal > 0
                        ? <Money minorUnits={stockVal} currencyCode={currencyCode} />
                        : <span className="ui-money ui-money--muted">—</span>}
                    </div>
                    {/* Действия */}
                    <div className="rowact">
                      <button
                        type="button"
                        className="ui-btn ui-btn--sm ui-btn--ghost"
                        disabled={!onReceive}
                        title={t('op.stock.action.receive')}
                        aria-label={t('op.stock.action.receive')}
                        onClick={() => onReceive?.(item.productId)}
                      ><Plus size={15} aria-hidden="true" /></button>
                      <button
                        type="button"
                        className="ui-btn ui-btn--sm ui-btn--danger"
                        disabled={item.stockOnHand <= 0}
                        title={t('op.stock.action.writeOff')}
                        aria-label={t('op.stock.action.writeOff')}
                        onClick={() => setWriteOffItem(item)}
                      ><Minus size={15} aria-hidden="true" /></button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      {writeOffItem && (
        <WriteOffDialog
          item={writeOffItem}
          backend={backend}
          currencyCode={currencyCode}
          onClose={() => setWriteOffItem(null)}
          onDone={() => { setWriteOffItem(null); setReloadNonce((n) => n + 1); onStockChanged?.(); }}
        />
      )}

      {/* ── Сводка: два героя + список к заказу ── */}
      <aside className="stock-summary">
        <StockHero
          label={t('op.stock.summary.totalValue')}
          value={<Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} />}
          sub={t('op.stock.summary.totalSub', { count: items.reduce((acc, i) => acc + Math.max(i.stockOnHand, 0), 0) })}
          tone="neutral"
        />

        <StockHero
          label={t('op.stock.summary.reorderTitle')}
          value={orderItems.length}
          sub={t('op.stock.summary.reorderSub', { low: summary.lowCount, out: summary.outCount })}
          tone={summary.outCount > 0 ? 'attention' : summary.lowCount > 0 ? 'warning' : 'muted'}
        />

        {orderItems.length > 0 && (
          <section className="stock-section">
            <h3 className="ctx-title">{t('op.stock.summary.orderTitle')}</h3>
            {orderItems.map((item) => {
              const s = stockStatus(item);
              return (
                <div key={item.productId} className="order-item" title={item.name}>
                  <strong className="order-item-name">{item.name}</strong>
                  <span className={`oq ${s}`}>{item.stockOnHand}/{item.reorderThreshold}</span>
                </div>
              );
            })}
            <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={!onReceive} onClick={() => onReceive?.()}>
              {t('op.stock.summary.orderBtn')}
            </button>
          </section>
        )}
      </aside>
    </div>
  );
}
