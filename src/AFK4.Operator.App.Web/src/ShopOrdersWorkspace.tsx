import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { createOperatorRealtimeClient } from './operatorRealtime';
import type { OperatorBackendContext } from './operatorTypes';
import type { ShopOrderDto } from './operatorApiClients';

const isOpenStatus = (status: string) => status === 'placed' || status === 'accepted';

function sortByPlacedAt(orders: ShopOrderDto[]): ShopOrderDto[] {
  return [...orders].sort((a, b) => a.placedAtUtc.localeCompare(b.placedAtUtc));
}

function upsertOrder(orders: ShopOrderDto[], order: ShopOrderDto, branchId: string): ShopOrderDto[] {
  const without = orders.filter((candidate) => candidate.id !== order.id);

  if (order.branchId !== branchId || !isOpenStatus(order.status)) {
    return without;
  }

  return sortByPlacedAt([...without, order]);
}

function applyAction(orders: ShopOrderDto[], updated: ShopOrderDto): ShopOrderDto[] {
  const without = orders.filter((candidate) => candidate.id !== updated.id);

  if (!isOpenStatus(updated.status)) {
    return without;
  }

  return sortByPlacedAt([...without, updated]);
}

export function ShopOrdersWorkspace({ backend }: { backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);

  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    [backend?.config, backend?.session]
  );

  useEffect(() => {
    let disposed = false;

    if (backend === null || clients === null) {
      setOrders([]);
      return undefined;
    }

    const branchId = backend.branchId;

    clients.shopOrders.listQueue(branchId)
      .then((queue) => {
        if (disposed) {
          return;
        }

        setOrders(sortByPlacedAt(queue.filter((order) => isOpenStatus(order.status))));
      })
      .catch(() => {
        if (disposed) {
          return;
        }

        setOrders([]);
      });

    const realtime = createOperatorRealtimeClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken,
      onDeviceStatusChanged: () => {},
      onShopOrderCreated: (order) => setOrders((current) => upsertOrder(current, order, branchId)),
      onShopOrderUpdated: (order) => setOrders((current) => upsertOrder(current, order, branchId))
    });
    void realtime.start();

    return () => {
      disposed = true;
      void realtime.stop();
    };
  }, [clients, backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const runAction = (
    order: ShopOrderDto,
    verb: 'accept' | 'deliver' | 'cancel'
  ) => async () => {
    if (backend === null || clients === null) {
      return;
    }

    try {
      const updated = await clients.shopOrders[verb](backend.branchId, order.id, order.version);
      setOrders((current) => applyAction(current, { ...order, ...updated }));
    } catch {
      // A 409 means another operator already acted; realtime reconciles the queue.
    }
  };

  return (
    <main className="workspace-screen shop-orders-screen">
      <section className="screen-head">
        <h1>{t('op.shopOrders.title')}</h1>
      </section>

      {orders.length === 0 ? (
        <p className="shop-orders-empty">{t('op.shopOrders.empty')}</p>
      ) : (
        <ul className="shop-orders-list">
          {orders.map((order) => (
            <li key={order.id} className="shop-order-card">
              <div className="shop-order-head">
                <strong>{order.playerDisplayName}</strong>
                <span className={`shop-order-status ${order.status}`}>
                  {order.status === 'accepted'
                    ? t('op.shopOrders.status.accepted')
                    : t('op.shopOrders.status.placed')}
                </span>
              </div>
              <div className="shop-order-meta">
                <span>{t('op.shopOrders.seat')} {order.seatId}</span>
                <span>{(order.total.minorUnits / 100).toFixed(2)} {order.total.currencyCode}</span>
              </div>
              <div className="shop-order-actions">
                {order.status === 'placed' && (
                  <button type="button" onClick={runAction(order, 'accept')}>
                    {t('op.shopOrders.accept')}
                  </button>
                )}
                {order.status === 'accepted' && (
                  <button type="button" onClick={runAction(order, 'deliver')}>
                    {t('op.shopOrders.deliver')}
                  </button>
                )}
                <button type="button" className="shop-order-cancel" onClick={runAction(order, 'cancel')}>
                  {t('op.shopOrders.cancel')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
