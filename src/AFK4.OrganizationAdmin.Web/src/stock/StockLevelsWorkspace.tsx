import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { AlertTriangle, Boxes, PackageMinus, Plus } from 'lucide-react';
import { useDeferredFlag } from '../useDeferredFlag';
import { EmptyState, Money, PartialLoadFailure } from '../operatorPrimitives';
import { StockSkeleton } from './StockSkeleton';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { PosProductCategoryDto, PosProductDto } from '../operatorApiClients';
import type { OperatorAuthSession } from '../authClient';
import { readCategoryDirectory } from '../posCategoryDirectory';
import {
  mapCatalogToStock,
  stockStatus,
  stockValueMinorUnits,
  summarize,
  type StockItem,
} from './stockLevels';
import { WriteOffDialog } from './WriteOffDialog';
import { FilterChip, IconButton, RowActions, StatusBadge, useBlockedReason } from '@afk4/ui/react';

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
  // Приёмку открывает тот, у кого есть на неё право; без него кнопка «Заказать» гасла молча. Строки
  // списка с тем же «+» не повторяют причину — хватит одной под итогом.
  const receiveBlocked = useBlockedReason(onReceive ? null : t('op.stock.receiveNoPermission'));

  const canView = hasPermission(session, permissionNames.viewInventory);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [categories, setCategories] = useState<PosProductCategoryDto[]>([]);
  const items = useMemo<StockItem[]>(() => mapCatalogToStock(catalog, readCategoryDirectory(categories)), [catalog, categories]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [categoriesError, setCategoriesError] = useState<OperatorErrorProjection | null>(null);
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
    setCategoriesError(null);
    // Каталог и справочник категорий берутся вместе: без второго у товара есть только
    // `categoryId`, и подпись категории на карточке не появлялась вовсе. Но остатки справочнику
    // не принадлежат: его отказ не прячет их, а называется рядом — раньше он молча становился
    // пустым справочником, и подписи пропадали без объяснения.
    Promise.allSettled([
      clients.pos.getCatalog(backend.branchId),
      clients.settings.listProductCategories(backend.branchId)
    ])
      .then(([loadedCatalog, loadedCategories]) => {
        if (!alive) return;
        if (loadedCatalog.status === 'fulfilled') setCatalog(loadedCatalog.value);
        else setLoadError(projectOperatorError(loadedCatalog.reason, t).detail);
        if (loadedCategories.status === 'fulfilled') setCategories(loadedCategories.value);
        else setCategoriesError(projectOperatorError(loadedCategories.reason, t));
      })
      .finally(() => {
        if (alive) setLoading(false);
      });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canView, reloadNonce, refreshNonce]);

  const retryCategories = () => {
    if (clients === null || backend === null) return;
    setCategoriesError(null);
    clients.settings.listProductCategories(backend.branchId)
      .then(setCategories)
      .catch((error) => setCategoriesError(projectOperatorError(error, t)));
  };

  const showSkeleton = useDeferredFlag(loading);

  if (!canView) {
    return (
      <section className="cash-stock-levels">
        <p className="ui-alert ui-alert--spaced">{t('op.stock.levels.noPermission')}</p>
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
          <p className="ui-alert ui-alert--spaced" role="alert">{loadError}</p>
        </section>
      </div>
    );
  }

  // Одно слово — одно состояние: «На исходе» — мало, но есть; «Нет в наличии» — ноль. Раньше
  // фильтр «На исходе» считал оба, а карточка рядом делила их на «мало» и «нет».
  const filtered = items.filter((item) => filter === 'all' || stockStatus(item) === filter).filter((item) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return item.name.toLowerCase().includes(q) || item.sku.toLowerCase().includes(q);
  });

  const summary = summarize(items);
  const orderItems = items.filter((i) => stockStatus(i) !== 'ok');
  const filters: { value: FilterMode; label: string; count: number }[] = [
    { value: 'all', label: t('op.stock.filter.all'), count: items.length },
    { value: 'low', label: t('op.stock.status.low'), count: summary.lowCount },
    { value: 'out', label: t('op.stock.status.out'), count: summary.outCount },
  ];

  return (
    <div className="stock-layout">
      {/* ── Список ── */}
      <section className="cash-stock-levels">
        {/* Имя вкладки «Остатки» уже в шапке раздела — здесь его не повторяем. */}
        <div className="levels-head">
          <div className="seg">
            {filters.map((option) => (
              <FilterChip key={option.value} label={option.label} count={option.count} pressed={filter === option.value} onClick={() => setFilter(option.value)} />
            ))}
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

        {categoriesError !== null && (
          <PartialLoadFailure text={t('op.stock.levels.categoriesFailed', { reason: categoriesError.detail })} failure={categoriesError} onRetry={retryCategories} />
        )}

        {/* Заголовки колонок */}
        <div className="cash-stock-cols srow" aria-hidden="true">
          <span />
          <span>{t('op.stock.col.item')}</span>
          <div className="metrics">
            <span>{t('op.stock.col.qty')}</span>
            <span>{t('op.stock.col.cost')}</span>
            <span>{t('op.stock.col.price')}</span>
            <span>{t('op.stock.col.value')}</span>
            <span />
          </div>
        </div>

        {items.length === 0 ? (
          // Склад считает только товары с учётом остатков. Приёмка их же и принимает, так что
          // звать туда отсюда — тупик: следующий шаг в карточке товара.
          <EmptyState
            icon={<Boxes size={28} aria-hidden="true" />}
            title={t('op.stock.levels.empty')}
            next={{ kind: 'elsewhere', hint: t('op.empty.trackStockWhere') }}
          />
        ) : filtered.length === 0 ? (
          <EmptyState
            icon={<Boxes size={28} aria-hidden="true" />}
            title={t('op.stock.levels.emptyFiltered')}
            next={{ kind: 'action', label: t('op.empty.resetFilter'), onClick: () => { setFilter('all'); setSearch(''); } }}
          />
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
                      {status === 'low' && <StatusBadge tone="warning">{t('op.stock.status.low')}</StatusBadge>}
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
                    {/* Одно частое действие на виду — принять товар; списание — в «⋯». Корзина
                        была списанием и читалась как «удалить товар». Без права на приёмку
                        кнопки нет вовсе, а не погашенной. */}
                    <div className="rowact">
                      {onReceive ? (
                        <IconButton size="sm" label={t('op.stock.action.receive')} icon={<Plus size={15} aria-hidden="true" />} onClick={() => onReceive(item.productId)} />
                      ) : null}
                      <RowActions
                        size="sm"
                        label={t('op.stock.row.more', { name: item.name })}
                        actions={[{
                          id: 'writeOff',
                          label: t('op.stock.action.writeOff'),
                          icon: <PackageMinus size={15} aria-hidden="true" />,
                          disabled: item.stockOnHand <= 0,
                          onSelect: () => setWriteOffItem(item),
                        }]}
                      />
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

      {/* ── Что дозаказать ── Стоимость склада — в шапке раздела, счётчики состояний — в фильтрах:
          две карточки здесь повторяли их же. Остаётся то, чего нет больше нигде: что заказать. */}
      {orderItems.length > 0 && (
      <aside className="stock-summary">
          <section className="stock-section">
            <h3 className="ctx-title">{t('op.stock.summary.orderTitle')}</h3>
            {orderItems.map((item) => {
              const s = stockStatus(item);
              // Порог не задан (0) → дробь "0/0" ничего не говорит, показываем статус словом.
              const qtyLabel = s === 'low' && item.reorderThreshold > 0
                ? t('op.stock.summary.lowOf', { count: item.stockOnHand, threshold: item.reorderThreshold })
                : t(s === 'out' ? 'op.stock.status.out' : 'op.stock.status.low');
              return (
                <div key={item.productId} className="order-item" title={item.name}>
                  <strong className="order-item-name">{item.name}</strong>
                  <span className={`oq ${s}`}>{qtyLabel}</span>
                </div>
              );
            })}
            <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={!onReceive} aria-describedby={receiveBlocked.describedBy} onClick={() => onReceive?.()}>
              {t('op.stock.summary.orderBtn')}
            </button>
            {receiveBlocked.hint}
          </section>
      </aside>
      )}
    </div>
  );
}
