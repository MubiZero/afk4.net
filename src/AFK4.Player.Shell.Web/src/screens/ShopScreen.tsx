import { useEffect, useState } from 'react';
import type { ShopCatalogItemDto, ShopOrderDto } from '../apiTypes';
import { createCachedLoader, indexedDbStore } from '../idbCache';
import { ApiError, OfflineError, type ShellApi } from '../shellApi';

export interface ShopScreenProps {
  api: ShellApi;
  onNeedTopUp: () => void;
  onDone: () => void;
  pollIntervalMs?: number;
}

function formatMinor(minorUnits: number): string {
  return `${(minorUnits / 100).toFixed(2)} с.`;
}

export function ShopScreen({ api, onNeedTopUp, onDone, pollIntervalMs = 4000 }: ShopScreenProps) {
  const [catalog, setCatalog] = useState<ShopCatalogItemDto[]>([]);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [order, setOrder] = useState<ShopOrderDto | null>(null);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = createCachedLoader(indexedDbStore(), 'shop-catalog', () => api.listShopCatalog());
    load().then(setCatalog).catch((e) => { if (e instanceof OfflineError) setOffline(true); });
  }, [api]);

  useEffect(() => {
    if (!order || order.status === 'delivered' || order.status === 'cancelled') return;
    const timer = setInterval(async () => {
      try {
        const mine = await api.listShopOrders();
        const found = mine.find((o) => o.id === order.id);
        if (found) setOrder(found);
      } catch { /* keep last known status; offline is transient */ }
    }, pollIntervalMs);
    return () => clearInterval(timer);
  }, [api, order?.id, order?.status, pollIntervalMs]);

  function add(item: ShopCatalogItemDto) {
    setCart((c) => ({ ...c, [item.productId]: (c[item.productId] ?? 0) + 1 }));
  }

  async function placeOrder() {
    const lines = Object.entries(cart)
      .filter(([, qty]) => qty > 0)
      .map(([productId, quantity]) => ({ productId, quantity }));
    if (lines.length === 0) return;
    setError(null);
    setBusy(true);
    try {
      setOrder(await api.placeShopOrder(lines));
      setCart({});
    } catch (e) {
      if (e instanceof ApiError && e.code === 'insufficient_funds') onNeedTopUp();
      else if (e instanceof OfflineError) setOffline(true);
      else if (e instanceof ApiError) {
        setError(
          e.code === 'out_of_stock' ? 'Товар закончился'
          : e.code === 'product_unavailable' ? 'Товар недоступен'
          : 'Не удалось оформить заказ'
        );
      }
    } finally {
      setBusy(false);
    }
  }

  async function cancel() {
    if (!order) return;
    try { setOrder(await api.cancelShopOrder(order.id)); } catch { /* ignore; status poll reconciles */ }
  }

  if (offline) return <p role="alert">Магазин временно недоступен — обратитесь к оператору</p>;

  if (order) {
    const label = order.status === 'placed' ? 'Заказ принят, готовим'
      : order.status === 'accepted' ? 'Оператор несёт ваш заказ'
      : order.status === 'delivered' ? 'Заказ доставлен'
      : 'Заказ отменён';
    return (
      <section>
        <h1>Ваш заказ</h1>
        <p>{label}</p>
        <p>Сумма: {formatMinor(order.total.minorUnits)}</p>
        {order.status === 'placed' && <button type="button" onClick={cancel}>Отменить заказ</button>}
        {(order.status === 'delivered' || order.status === 'cancelled') &&
          <button type="button" onClick={onDone}>Готово</button>}
      </section>
    );
  }

  const cartCount = Object.values(cart).reduce((sum, qty) => sum + qty, 0);
  return (
    <section>
      <h1>Магазин</h1>
      <ul>
        {catalog.map((item) => (
          <li key={item.productId}>
            <span>{item.name}</span>
            <span>{formatMinor(item.price.minorUnits)}</span>
            <button type="button" onClick={() => add(item)}>Добавить</button>
            {cart[item.productId] ? <span aria-label={`в корзине: ${item.name}`}>×{cart[item.productId]}</span> : null}
          </li>
        ))}
      </ul>
      <button type="button" onClick={placeOrder} disabled={cartCount === 0 || busy}>Заказать</button>
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
