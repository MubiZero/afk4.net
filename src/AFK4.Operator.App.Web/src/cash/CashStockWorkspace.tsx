import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { AlertTriangle, Boxes } from 'lucide-react';
import { createAuthenticatedOperatorClients, createIdempotencyKey, readNumber, readString } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

interface StockItem {
  productId: string;
  name: string;
  sku: string;
  stockOnHand: number;
}

const LOW_STOCK_THRESHOLD = 2;

// Вкладка «Склад»: остатки по товарам + списание (переехало из «Быстрых операций» POS). Списание —
// это движение склада с отрицательной дельтой (adjustment) через inventory.createStockMovement.
export function CashStockWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session]
  );
  const canManage = hasPermission(session, permissionNames.manageInventoryStock);

  const [items, setItems] = useState<StockItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [productId, setProductId] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [reason, setReason] = useState(() => t('op.pos.defaultWriteOffReason'));
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);

  const load = (active: () => boolean) => {
    if (clients === null || backend === null) { setLoading(false); return; }
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((catalog) => {
        if (!active()) return;
        const stock = catalog
          .filter((product) => Boolean(product.trackStock) && readString(product, 'productId'))
          .map((product) => ({
            productId: readString(product, 'productId'),
            name: readString(product, 'name'),
            sku: readString(product, 'sku', 'SKU'),
            stockOnHand: readNumber(product, 'stockOnHand', 0)
          }));
        setItems(stock);
        setProductId((current) => current || stock[0]?.productId || '');
      })
      .catch((error) => { if (active()) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active()) setLoading(false); });
  };

  useEffect(() => {
    let alive = true;
    load(() => alive);
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId]);

  const writeOff = async () => {
    if (clients === null || backend === null) return;
    const qty = Number(quantity);
    const trimmedReason = reason.trim();
    if (!productId || !Number.isInteger(qty) || qty <= 0 || !trimmedReason) {
      setNotice({ kind: 'error', text: t('op.pos.error.invalidStockInput') });
      return;
    }
    setBusy(true);
    setNotice(null);
    try {
      await clients.inventory.createStockMovement(backend.branchId, {
        organizationId: backend.session.organizationId,
        productId,
        movementType: 'adjustment',
        quantityDelta: -qty,
        unitCost: { currencyCode, minorUnits: 0 },
        reason: trimmedReason,
        idempotencyKey: createIdempotencyKey('stock-write-off')
      });
      setNotice({ kind: 'ok', text: t('op.pos.feedback.stockWriteOff') });
      let alive = true;
      load(() => alive);
    } catch (error) {
      setNotice({ kind: 'error', text: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return <main className="workspace-screen cash-stock-screen"><p className="workspace-loading">{t('op.shifts.loading')}</p></main>;
  }
  if (loadError) {
    return <main className="workspace-screen cash-stock-screen"><p className="workspace-error" role="alert">{loadError}</p></main>;
  }

  return (
    <main className="workspace-screen cash-stock-screen">
      {canManage && (
        <section className="cash-stock-writeoff">
          <h2>{t('op.cash.stock.writeOffTitle')}</h2>
          <div className="cash-stock-form">
            <label>
              <span>{t('op.pos.quick.writeOffLabel')}</span>
              <select aria-label={t('op.pos.quick.writeOffProductLabel')} value={productId} disabled={items.length === 0 || busy} onChange={(event) => setProductId(event.currentTarget.value)}>
                {items.length === 0 && <option value="">{t('op.pos.quick.writeOffNoProduct')}</option>}
                {items.map((item) => (
                  <option key={item.productId} value={item.productId}>{t('op.pos.quick.writeOffProductItem', { name: item.name, count: item.stockOnHand })}</option>
                ))}
              </select>
            </label>
            <label>
              <span>{t('op.pos.quick.writeOffQtyLabel')}</span>
              <input aria-label={t('op.pos.quick.writeOffQtyAriaLabel')} inputMode="numeric" value={quantity} disabled={busy} onChange={(event) => setQuantity(event.currentTarget.value)} />
            </label>
            <label className="cash-stock-reason">
              <span>{t('op.pos.quick.writeOffReasonLabel')}</span>
              <input aria-label={t('op.pos.quick.writeOffReasonAriaLabel')} value={reason} disabled={busy} onChange={(event) => setReason(event.currentTarget.value)} />
            </label>
            <button type="button" disabled={items.length === 0 || busy} onClick={() => void writeOff()}>
              <AlertTriangle size={14} aria-hidden="true" />{t('op.pos.quick.writeOffBtn')}
            </button>
          </div>
          {notice && <p className={notice.kind === 'ok' ? 'cash-stock-ok' : 'cash-export-error'} role={notice.kind === 'error' ? 'alert' : undefined}>{notice.text}</p>}
        </section>
      )}

      <section className="cash-stock-levels">
        <h2>{t('op.cash.stock.levelsTitle')}</h2>
        {items.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.cash.stock.empty')}</p>
        ) : (
          <ul className="cash-stock-list">
            {items.map((item) => {
              const low = item.stockOnHand <= LOW_STOCK_THRESHOLD;
              return (
                <li key={item.productId} className={`cash-stock-row${low ? ' low' : ''}`}>
                  <Boxes size={15} aria-hidden="true" />
                  <strong>{item.name}</strong>
                  <em>{item.sku}</em>
                  <b>{t('op.cash.stock.onHand', { count: item.stockOnHand })}</b>
                  {low && <span className="cash-stock-low-tag">{t('op.cash.stock.lowTag')}</span>}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </main>
  );
}
