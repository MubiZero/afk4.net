import { useState } from 'react';
import { Minus, Plus, WifiOff } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button, Money } from '@afk4/ui/react';
import { formatTime, parseNonNegativeMoneyInputMinorUnits } from '../operatorHelpers';
import { useToast } from '../operatorToast';
import type { OfflineProduct } from './offlineCache';
import { useOfflineDeskContext, type OfflineDesk } from './useOfflineDesk';

/**
 * Касса без связи (docs/roadmap/offline-front-desk.md): продать товар за наличные и сдать смену. Цены —
 * из кэша последней связи, записи ложатся в журнал на диске и уйдут на сервер, когда связь вернётся.
 * Ящик считают вслепую: сколько должно быть и расхождение посчитает сервер по всем деньгам смены.
 */
export function OfflineCashScreen() {
  const { t } = useI18n();
  const desk = useOfflineDeskContext();
  if (!desk) return null;

  return (
    <main className="workspace-screen offline-cash" aria-label={t('op.offline.title')}>
      <header className="offline-seat offline-cash-header">
        <p className="offline-seat-title"><WifiOff aria-hidden="true" size={14} /> {t('op.offline.title')}</p>
        {desk.unavailableReason === 'no-host' ? (
          <p className="offline-seat-hint">{t('op.offline.reason.noHost')}</p>
        ) : (
          <p className="offline-seat-hint">
            {desk.cachedAtMs !== null && t('op.offline.cash.pricesFrom', { time: formatTime(new Date(desk.cachedAtMs).toISOString()) })}
            {desk.pendingCount > 0 && ` ${t('op.offline.cash.pending', { count: desk.pendingCount })}`}
          </p>
        )}
      </header>
      {desk.unavailableReason !== 'no-host' && (
        <div className="offline-cash-columns">
          {desk.shiftId !== null && <OfflineSale desk={desk} />}
          <OfflineShift desk={desk} />
        </div>
      )}
    </main>
  );
}

function OfflineSale({ desk }: { desk: OfflineDesk }) {
  const { t } = useI18n();
  const toast = useToast();
  const [cart, setCart] = useState<Map<string, number>>(new Map());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!desk.canSell) {
    return <section className="offline-cash-panel"><h2>{t('op.offline.cash.goods')}</h2><p className="offline-seat-hint">{t('op.offline.cash.cannotSell')}</p></section>;
  }

  const byId = new Map(desk.products.map((product) => [product.productId, product]));
  const lines = [...cart].flatMap(([productId, quantity]) => {
    const product = byId.get(productId);
    return product ? [{ product, quantity }] : [];
  });
  const total = lines.reduce((sum, line) => sum + line.product.priceMinorUnits * line.quantity, 0);
  const change = (product: OfflineProduct, delta: number) => setCart((current) => {
    const next = new Map(current);
    const quantity = (next.get(product.productId) ?? 0) + delta;
    if (quantity > 0) next.set(product.productId, quantity);
    else next.delete(product.productId);
    return next;
  });

  const take = async () => {
    setBusy(true);
    setError(null);
    try {
      await desk.sell(lines.map(({ product, quantity }) => ({
        productId: product.productId, name: product.name, quantity, unitPriceMinorUnits: product.priceMinorUnits
      })));
      setCart(new Map());
      toast.success(t('op.offline.cash.sold'));
    } catch (failure) {
      setError(t('op.offline.failed', { reason: failure instanceof Error ? failure.message : String(failure) }));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="offline-cash-panel" aria-label={t('op.offline.cash.goods')}>
      <h2>{t('op.offline.cash.goods')}</h2>
      {desk.products.length === 0 ? (
        <p className="offline-seat-hint">{t('op.offline.cash.noProducts')}</p>
      ) : (
        <div className="offline-cash-products">
          {desk.products.map((product) => (
            <Button key={product.productId} size="sm" disabled={busy} onClick={() => change(product, 1)}>
              {product.name} · <Money minorUnits={product.priceMinorUnits} currencyCode={product.currencyCode} />
            </Button>
          ))}
        </div>
      )}
      {lines.length === 0 ? (
        <p className="offline-seat-hint">{t('op.offline.cash.cartEmpty')}</p>
      ) : (
        <ul className="offline-cash-cart">
          {lines.map(({ product, quantity }) => (
            <li key={product.productId}>
              <span>{product.name}</span>
              <Button size="sm" variant="secondary" aria-label={t('op.offline.cash.decrease', { name: product.name })} disabled={busy} onClick={() => change(product, -1)}>
                <Minus aria-hidden="true" size={14} />
              </Button>
              <span aria-live="polite">{quantity}</span>
              <Button size="sm" variant="secondary" aria-label={t('op.offline.cash.increase', { name: product.name })} disabled={busy} onClick={() => change(product, 1)}>
                <Plus aria-hidden="true" size={14} />
              </Button>
              <Money minorUnits={product.priceMinorUnits * quantity} currencyCode={product.currencyCode} />
            </li>
          ))}
        </ul>
      )}
      <Button variant="primary" block disabled={busy || lines.length === 0} onClick={() => void take()}>
        {t('op.offline.cash.take')} · <Money minorUnits={total} currencyCode={desk.currencyCode} />
      </Button>
      {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
    </section>
  );
}

function OfflineShift({ desk }: { desk: OfflineDesk }) {
  const { t } = useI18n();
  const toast = useToast();
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const minorUnits = parseNonNegativeMoneyInputMinorUnits(amount);
  const isOpen = desk.shiftId !== null;

  const submit = async () => {
    if (minorUnits === null) {
      setError(t('op.offline.cash.invalidAmount'));
      return;
    }

    if (isOpen && !confirming) {
      setConfirming(true);
      return;
    }

    setBusy(true);
    setError(null);
    try {
      if (isOpen) {
        await desk.closeShift(minorUnits, note.trim());
        toast.success(t('op.offline.cash.closed'));
      } else {
        await desk.openShift(minorUnits, note.trim());
        toast.success(t('op.offline.cash.opened'));
      }
      setAmount('');
      setNote('');
      setConfirming(false);
    } catch (failure) {
      setError(t('op.offline.failed', { reason: failure instanceof Error ? failure.message : String(failure) }));
    } finally {
      setBusy(false);
    }
  };

  const allowed = isOpen ? desk.canCloseShift : desk.canOpenShift;
  return (
    <section className="offline-cash-panel" aria-label={t('op.offline.cash.shift')}>
      <h2>{t('op.offline.cash.shift')}</h2>
      {!isOpen && <p className="offline-seat-hint">{t('op.offline.cash.noShift')}</p>}
      {!allowed ? (
        <p className="offline-seat-hint">{t(isOpen ? 'op.offline.cash.cannotClose' : 'op.offline.cash.cannotOpen')}</p>
      ) : (
        <>
          {isOpen && <p className="offline-seat-hint">{t('op.offline.cash.blindHint')}</p>}
          <label className="offline-seat-field">
            {t(isOpen ? 'op.offline.cash.countedLabel' : 'op.offline.cash.startingLabel')}
            <input
              inputMode="decimal"
              value={amount}
              disabled={busy}
              onChange={(event) => { setAmount(event.currentTarget.value); setConfirming(false); }}
            />
          </label>
          <label className="offline-seat-field">
            {t('op.offline.cash.noteLabel')}
            <input value={note} disabled={busy} onChange={(event) => setNote(event.currentTarget.value)} />
          </label>
          <Button variant={isOpen && confirming ? 'danger' : 'primary'} block disabled={busy || amount.trim() === ''} onClick={() => void submit()}>
            {isOpen
              ? confirming && minorUnits !== null
                ? <>{t('op.offline.cash.confirmClose')} · <Money minorUnits={minorUnits} currencyCode={desk.currencyCode} /></>
                : t('op.offline.cash.close')
              : t('op.offline.cash.open')}
          </Button>
        </>
      )}
      {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
    </section>
  );
}
