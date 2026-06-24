import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatMinorUnits } from './operatorHelpers';
import { createOperatorRealtimeClient } from './operatorRealtime';
import { projectOperatorError } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { ShopOrderDto } from './operatorApiClients';
import { EmptyState } from './operatorPrimitives';
import { useToast } from './operatorToast';

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

export function ShopOrdersWorkspace({ backend, embedded = false }: { backend: OperatorBackendContext | null; embedded?: boolean }) {
  const { t } = useI18n();
  const toast = useToast();
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);

  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    [backend?.config, backend?.session]
  );

  useEffect(() => {
    let disposed = false;

    if (backend === null || clients === null) {
      setOrders([]);
      setStatus('ready');
      return undefined;
    }

    const branchId = backend.branchId;

    setStatus('loading');
    setLoadError(null);
    clients.shopOrders.listQueue(branchId)
      .then((queue) => {
        if (disposed) {
          return;
        }

        setOrders(sortByPlacedAt(queue.filter((order) => isOpenStatus(order.status))));
        setStatus('ready');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setOrders([]);
        setLoadError(projectOperatorError(error, t).detail);
        setStatus('error');
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
      toast.success(t(`op.shopOrders.toast.${verb}`));
    } catch (error) {
      // Surface the failure like sibling workspaces do. A 409 means another operator already
      // acted; realtime still reconciles the queue — the toast just tells the operator why.
      toast.error(projectOperatorError(error, t).detail);
    }
  };

  const body = (
    <>
      {!embedded && (
        <section className="screen-head">
          <h1>{t('op.shopOrders.title')}</h1>
        </section>
      )}

      {status === 'loading' ? (
        <p className="workspace-loading">{t('op.shopOrders.loading')}</p>
      ) : status === 'error' ? (
        <p className="workspace-error" role="alert">{loadError ?? t('op.shopOrders.error')}</p>
      ) : orders.length === 0 ? (
        <EmptyState title={t('op.shopOrders.empty')} className="shop-orders-empty" />
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
                <span>{formatMinorUnits(order.total.minorUnits, order.total.currencyCode)}</span>
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
    </>
  );

  return embedded
    ? <section className="shop-orders-embed">{body}</section>
    : <main className="workspace-screen shop-orders-screen">{body}</main>;
}
