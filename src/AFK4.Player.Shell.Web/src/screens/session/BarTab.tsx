import { useCallback, useEffect, useRef, useState } from 'react';
import { ShopOrderStatusNames, type MoneyDto, type ShopCatalogItemDto, type ShopOrderDto } from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { Minus, Plus } from 'lucide-react';
import { getJson, postJson } from '../../api/playerApi';
import {
  ORDER_STATUS_KEYS,
  barErrorKey,
  cartLines,
  cartTotalMinor,
  changeQuantity,
  fewLeft,
  isOrderActive,
  type Cart
} from '../../model/bar';
import { INTL_LOCALES } from '../../model/offers';

/** Как часто спрашивать, где заказ: стойка отвечает за минуты, а не за секунды. */
const ORDER_POLL_MS = 10_000;

/**
 * Бар к месту (группа 1 решений по Панели): меню клуба, заказ с кошелька, статус и отмена, пока
 * заказ не начали готовить. Деньги списываются при заказе и возвращаются при отмене — так же, как
 * в приложении; экран говорит об этом словами, а не молчит.
 */
export function BarTab({ baseUrl }: { baseUrl: string }) {
  const { t, locale } = useI18n();
  const [catalog, setCatalog] = useState<ShopCatalogItemDto[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [cart, setCart] = useState<Cart>({});
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);
  const [placing, setPlacing] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);
  const [cancelling, setCancelling] = useState<string | null>(null);
  const idempotencyKey = useRef(crypto.randomUUID());

  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, INTL_LOCALES[locale]);

  const loadCatalog = useCallback(async () => {
    setFailed(false);
    try {
      const items = await getJson<ShopCatalogItemDto[]>(baseUrl, '/api/me/shop/catalog');
      // Ответ не того вида не должен уронить экран игрока — это «меню не загрузилось».
      if (!Array.isArray(items)) throw new Error('Unexpected catalog response.');
      setCatalog(items);
    } catch {
      setFailed(true);
    }
  }, [baseUrl]);

  const loadOrders = useCallback(async () => {
    try {
      const list = await getJson<ShopOrderDto[]>(baseUrl, '/api/me/shop/orders');
      if (Array.isArray(list)) setOrders(list);
    } catch {
      // Статус заказа подтянется на следующем круге; меню от этого не ломается.
    }
  }, [baseUrl]);

  useEffect(() => {
    void loadCatalog();
    void loadOrders();
  }, [loadCatalog, loadOrders]);

  const active = orders.filter(isOrderActive);
  const hasActive = active.length > 0;
  useEffect(() => {
    if (!hasActive) return;
    const timer = window.setInterval(() => void loadOrders(), ORDER_POLL_MS);
    return () => window.clearInterval(timer);
  }, [hasActive, loadOrders]);

  const place = async () => {
    if (placing || !catalog) return;
    setPlacing(true);
    setError(null);
    try {
      const order = await postJson<ShopOrderDto>(baseUrl, '/api/me/shop/orders', {
        lines: cartLines(cart),
        idempotencyKey: idempotencyKey.current
      });
      setOrders((current) => [order, ...current.filter((existing) => existing.id !== order.id)]);
      setCart({});
      idempotencyKey.current = crypto.randomUUID();
    } catch (reason) {
      const key = barErrorKey(reason);
      setError(key);
      if (key === 'playerShell.bar.error.stock' || key === 'playerShell.bar.error.unavailable') {
        idempotencyKey.current = crypto.randomUUID();
        void loadCatalog();
      }
    } finally {
      setPlacing(false);
    }
  };

  const cancel = async (order: ShopOrderDto) => {
    setCancelling(order.id);
    setError(null);
    try {
      const updated = await postJson<ShopOrderDto>(baseUrl, `/api/me/shop/orders/${order.id}/cancel`, {});
      setOrders((current) => current.map((existing) => (existing.id === updated.id ? updated : existing)));
    } catch {
      setError('playerShell.bar.cancelFailed');
      void loadOrders();
    } finally {
      setCancelling(null);
    }
  };

  // Только что отменённый или принесённый заказ остаётся на виду один круг — человек видит итог.
  const recent = orders.filter((order) => !isOrderActive(order)).slice(0, 1);
  const shown = [...active, ...recent.filter(() => active.length === 0)];
  const totalMinor = catalog ? cartTotalMinor(cart, catalog) : 0;
  const currency = catalog?.[0]?.price.currencyCode ?? 'TJS';

  return (
    <div className="bar">
      {shown.map((order) => (
        <article key={order.id} className="bar-order" aria-live="polite">
          <header className="bar-order__head">
            <h3 className="bar-order__title">{t('playerShell.bar.orderTitle')}</h3>
            <span className="bar-order__total mono">{money(order.total)}</span>
          </header>
          <p className="bar-order__lines">
            {order.lines.map((line) => `${line.name} × ${line.quantity}`).join(', ')}
          </p>
          <p className={`bar-order__status bar-order__status--${order.status}`}>{t(ORDER_STATUS_KEYS[order.status] ?? 'playerShell.bar.status.placed')}</p>
          {order.status === ShopOrderStatusNames.Placed ? (
            <button type="button" className="btn btn--ghost" disabled={cancelling === order.id} onClick={() => void cancel(order)}>
              {t('playerShell.bar.cancel')}
            </button>
          ) : null}
        </article>
      ))}

      {catalog ? (
        catalog.length === 0 ? (
          <p className="bar__empty">{t('playerShell.bar.empty')}</p>
        ) : (
          <ul className="bar__menu">
            {catalog.map((item) => {
              const quantity = cart[item.productId] ?? 0;
              return (
                <li key={item.productId} className="bar-item">
                  <div className="bar-item__info">
                    <span className="bar-item__name">{item.name}</span>
                    <span className="bar-item__price mono">{money(item.price)}</span>
                    {fewLeft(item) ? (
                      <span className="bar-item__few">{t('playerShell.bar.lastItems', { count: item.stockOnHand })}</span>
                    ) : null}
                  </div>
                  <div className="bar-item__quantity">
                    <button
                      type="button"
                      className="bar-item__step"
                      aria-label={t('playerShell.bar.remove', { name: item.name })}
                      disabled={quantity === 0 || placing}
                      onClick={() => setCart((current) => changeQuantity(current, item.productId, -1))}
                    >
                      <Minus aria-hidden="true" />
                    </button>
                    <span className="bar-item__count mono" aria-live="polite">{quantity}</span>
                    <button
                      type="button"
                      className="bar-item__step"
                      aria-label={t('playerShell.bar.add', { name: item.name })}
                      disabled={placing}
                      onClick={() => setCart((current) => changeQuantity(current, item.productId, 1))}
                    >
                      <Plus aria-hidden="true" />
                    </button>
                  </div>
                </li>
              );
            })}
          </ul>
        )
      ) : failed ? (
        <div className="offers__failed" role="alert">
          <p>{t('playerShell.bar.loadFailed')}</p>
          <button type="button" className="btn btn--ghost" onClick={() => void loadCatalog()}>{t('playerShell.chooseTime.retry')}</button>
        </div>
      ) : (
        <ul className="bar__menu" aria-hidden="true">
          {[0, 1, 2, 3].map((index) => <li key={index} className="bar-item skeleton" />)}
        </ul>
      )}

      {error ? <p className="bar__error" role="alert">{t(error)}</p> : null}
      {catalog && catalog.length > 0 ? (
        <footer className="bar__action">
          <button type="button" className="btn btn--primary" disabled={totalMinor === 0 || placing} onClick={() => void place()}>
            {placing
              ? t('playerShell.bar.placing')
              : totalMinor === 0
                ? t('playerShell.bar.pick')
                : t('playerShell.bar.place', { amount: money({ currencyCode: currency, minorUnits: totalMinor }) })}
          </button>
        </footer>
      ) : null}
    </div>
  );
}
