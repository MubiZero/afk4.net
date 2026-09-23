import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Check, ChevronRight, Clock, HandPlatter } from 'lucide-react';
import { createAuthenticatedOperatorClients, formatMinorUnits } from './operatorHelpers';
import { createOperatorRealtimeClient, createPreviewOperatorRealtimeClient } from './operatorRealtime';
import { projectOperatorError } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { ShopOrderDto } from './operatorApiClients';
import { useToast } from './operatorToast';

const isOpenStatus = (status: string) => status === 'placed' || status === 'accepted';
const sortByPlacedAt = (orders: ShopOrderDto[]): ShopOrderDto[] =>
  [...orders].sort((a, b) => a.placedAtUtc.localeCompare(b.placedAtUtc));

// Очередь держим только из открытых заказов текущего филиала: доставленные/отменённые и чужие
// филиалы выпадают. Сортировка по времени размещения — старейший заказ слева (его обслужить раньше).
function reconcile(orders: ShopOrderDto[], order: ShopOrderDto, branchId: string): ShopOrderDto[] {
  const without = orders.filter((candidate) => candidate.id !== order.id);
  if (order.branchId !== branchId || !isOpenStatus(order.status)) return without;
  return sortByPlacedAt([...without, order]);
}

const POPOVER_MARGIN = 8;
const POPOVER_CLOSE_MS = 140;
const prefersReducedMotion = () =>
  typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;

// Место так, как его зовут вслух. Место могли убрать с карты зала — тогда заказ узнают по гостю.
const seatLabel = (order: ShopOrderDto): string => order.seatName ?? order.playerDisplayName;

// Лента входящих заказов из Player Shell поверх POS: всегда на виду, обновляется realtime (SignalR).
// Чип компактен (место + «N поз · сумма»), клик по нему раскрывает поповер со ВСЕМ составом.
// На пике лента — FIFO-очередь: старые слева (sortByPlacedAt), счётчик в лейбле, горизонтальный
// скролл с видимым скроллбаром и правой тенью-подсказкой «есть ещё».
export function PosOrdersTicker({ backend, canCancel, openOrder }: {
  backend: OperatorBackendContext | null;
  /// Отмена возвращает деньги, поэтому спрашивает право сильнее, чем вся остальная лента.
  /// Принять и выдать заказ может кассир, вернуть за него деньги — нет.
  canCancel: boolean;
  /// Заказ из командной палитры: поповер открывается сразу на нём. Грузится по идентификатору —
  /// лента держит только заказы в работе, а приходят и с выданным.
  openOrder?: { orderId: string } | null;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [popover, setPopover] = useState<{ id: string; x: number; y: number } | null>(null);
  // Заказ, открытый из палитры: его может не быть в ленте (выдан, отменён), и поповер держится на нём.
  const [pinned, setPinned] = useState<ShopOrderDto | null>(null);
  const [closing, setClosing] = useState(false);
  const popoverRef = useRef<HTMLDivElement | null>(null);
  const tickerRef = useRef<HTMLElement | null>(null);
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session]
  );

  useEffect(() => {
    let disposed = false;
    if (backend === null || clients === null) {
      setOrders([]);
      return undefined;
    }
    const branchId = backend.branchId;
    setLoadError(null);
    clients.shopOrders.listQueue(branchId)
      .then((queue) => { if (!disposed) setOrders(sortByPlacedAt(queue.filter((order) => isOpenStatus(order.status)))); })
      .catch((error) => { if (!disposed) { setOrders([]); setLoadError(projectOperatorError(error, t).detail); } });

    const createRealtimeClient = backend.config.shellMode === 'vite-dev-preview'
      ? createPreviewOperatorRealtimeClient
      : createOperatorRealtimeClient;
    const realtime = createRealtimeClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken,
      onDeviceStatusChanged: () => {},
      onShopOrderCreated: (order) => setOrders((current) => reconcile(current, order, branchId)),
      onShopOrderUpdated: (order) => {
        setOrders((current) => reconcile(current, order, branchId));
        setPinned((current) => (current?.id === order.id ? order : current));
      }
    });
    void realtime.start();

    return () => { disposed = true; void realtime.stop(); };
  }, [clients, backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  // Закрытие поповера с exit-анимацией: помечаем closing, размонтируем по таймеру (или сразу при
  // reduced-motion). Открытие нового заказа отменяет ожидающее закрытие.
  const cancelCloseTimer = () => {
    if (closeTimer.current !== null) {
      clearTimeout(closeTimer.current);
      closeTimer.current = null;
    }
  };
  const requestClose = () => {
    if (popover === null) return;
    setClosing(true);
    cancelCloseTimer();
    closeTimer.current = setTimeout(() => {
      setPopover(null);
      setPinned(null);
      setClosing(false);
      closeTimer.current = null;
    }, prefersReducedMotion() ? 0 : POPOVER_CLOSE_MS);
  };
  useEffect(() => () => cancelCloseTimer(), []);

  // Заказ из палитры. Поповер встаёт под его чипом, если заказ в ленте, иначе — под самой лентой.
  useEffect(() => {
    if (!openOrder || backend === null || clients === null) return undefined;
    let disposed = false;
    clients.shopOrders.get(backend.branchId, openOrder.orderId)
      .then((order) => {
        if (disposed) return;
        const chip = Array.from(tickerRef.current?.querySelectorAll<HTMLElement>('[data-order-id]') ?? [])
          .find((candidate) => candidate.dataset.orderId === order.id);
        const anchor = (chip ?? tickerRef.current)?.getBoundingClientRect();
        cancelCloseTimer();
        setClosing(false);
        setPinned(order);
        setPopover({ id: order.id, x: anchor?.left ?? POPOVER_MARGIN, y: (anchor?.bottom ?? POPOVER_MARGIN) + 4 });
      })
      .catch((error) => { if (!disposed) toast.error(projectOperatorError(error, t).detail); });
    return () => { disposed = true; };
    // Новый выбор в палитре — новый объект, даже если заказ тот же: открываем заново.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openOrder, clients, backend?.branchId]);

  const runAction = (order: ShopOrderDto, verb: 'accept' | 'deliver' | 'cancel') => async () => {
    if (backend === null || clients === null) return;
    try {
      const updated = await clients.shopOrders[verb](backend.branchId, order.id, order.version);
      setOrders((current) => reconcile(current, { ...order, ...updated }, backend.branchId));
      setPinned((current) => (current?.id === order.id ? { ...order, ...updated } : current));
      toast.success(t(`op.shopOrders.toast.${verb}`));
    } catch (error) {
      // 409 = другой оператор уже обработал; realtime сверит очередь, тост лишь объясняет почему.
      toast.error(projectOperatorError(error, t).detail);
    }
  };

  // Живой заказ под открытым поповером (берём из текущей очереди, чтобы поповер отражал realtime).
  // Заказ из палитры держится и вне очереди: с выданным и приходят.
  const popoverOrder = popover
    ? orders.find((order) => order.id === popover.id) ?? (pinned?.id === popover.id ? pinned : null)
    : null;

  // Если заказ ушёл из очереди (выдан/отменён, в т.ч. другим оператором) — закрываем поповер.
  useEffect(() => {
    if (popover !== null && !closing && popoverOrder === null) requestClose();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popover, closing, popoverOrder]);

  // Клик вне поповера / Escape — закрыть.
  useEffect(() => {
    if (popover === null) return undefined;
    const onPointerDown = (event: PointerEvent) => {
      if (!popoverRef.current?.contains(event.target as Node | null)) requestClose();
    };
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') requestClose(); };
    document.addEventListener('pointerdown', onPointerDown, true);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown, true);
      document.removeEventListener('keydown', onKeyDown);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [popover]);

  // Кламп поповера в видимую область — не вылезает за край экрана.
  useLayoutEffect(() => {
    if (popover === null) return;
    const node = popoverRef.current;
    if (node === null) return;
    const rect = node.getBoundingClientRect();
    let x = popover.x;
    let y = popover.y;
    if (x + rect.width + POPOVER_MARGIN > window.innerWidth) x = Math.max(POPOVER_MARGIN, window.innerWidth - rect.width - POPOVER_MARGIN);
    if (y + rect.height + POPOVER_MARGIN > window.innerHeight) y = Math.max(POPOVER_MARGIN, window.innerHeight - rect.height - POPOVER_MARGIN);
    if (x !== popover.x || y !== popover.y) setPopover({ id: popover.id, x, y });
  }, [popover]);

  const togglePopover = (order: ShopOrderDto, anchor: HTMLElement) => {
    if (popover?.id === order.id && !closing) {
      requestClose();
      return;
    }
    cancelCloseTimer();
    setClosing(false);
    const rect = anchor.getBoundingClientRect();
    setPopover({ id: order.id, x: rect.left, y: rect.bottom + 4 });
  };

  const closeAfter = (run: () => void) => () => { run(); requestClose(); };

  // Чип показывает «N поз · сумма»: ровно для любого размера заказа, плюс сумма как сигнал
  // приоритета (заказ на 200 ≠ на 12). Полный состав позиций — в поповере по клику.
  const chipSummary = (order: ShopOrderDto): string =>
    `${t('op.pos.order.positions', { count: order.lines.length })} · ${formatMinorUnits(order.total.minorUnits, order.total.currencyCode)}`;

  const statusLabel = (status: string) => {
    switch (status) {
      case 'accepted': return t('op.shopOrders.status.accepted');
      case 'delivered': return t('op.shopOrders.status.delivered');
      case 'cancelled': return t('op.shopOrders.status.cancelled');
      default: return t('op.shopOrders.status.placed');
    }
  };

  return (
    <section ref={tickerRef} className="pos-orders-ticker" aria-label={t('op.cash.sales.segOrders')}>
      <span className="pos-orders-ticker-label">
        {t('op.cash.sales.segOrders')}{orders.length > 0 ? ` · ${orders.length}` : ''}
      </span>
      {loadError ? (
        <span className="pos-orders-ticker-empty" role="alert">{loadError}</span>
      ) : orders.length === 0 ? (
        <span className="pos-orders-ticker-empty">{t('op.shopOrders.empty')}</span>
      ) : (
        <ul className="pos-orders-ticker-list">
          {orders.map((order) => (
            <li key={order.id} className={`pos-order-chip ${order.status}`} data-order-id={order.id}>
              <button
                type="button"
                className="pos-order-open"
                aria-haspopup="dialog"
                aria-expanded={popover?.id === order.id}
                onClick={(event) => togglePopover(order, event.currentTarget)}
                aria-label={`${seatLabel(order)} · ${statusLabel(order.status)} · ${chipSummary(order)}`}
              >
                {/* Раньше «новый» и «принят» отличались только цветом точки, а сама точка была
                    спрятана от читалки: за стойкой в час пик это различие теряется первым. */}
                <span className="pos-order-dot" aria-hidden="true">
                  {order.status === 'accepted' ? <Check size={10} /> : <Clock size={10} />}
                </span>
                <span className="pos-order-seat">{seatLabel(order)}</span>
                <span className="pos-order-items">{chipSummary(order)}</span>
                <ChevronRight
                  className={`pos-order-chevron${popover?.id === order.id ? ' is-expanded' : ''}`}
                  size={14}
                  aria-hidden="true"
                />
              </button>
            </li>
          ))}
        </ul>
      )}

      {popoverOrder && popover && (
        <div
          ref={popoverRef}
          className={`pos-order-detail${closing ? ' pos-order-detail--closing' : ''}`}
          role="dialog"
          aria-label={`${seatLabel(popoverOrder)} · ${statusLabel(popoverOrder.status)}`}
          style={{ left: popover.x, top: popover.y }}
        >
          <header className="pos-order-detail-head">
            <strong>{seatLabel(popoverOrder)}</strong>
            <span className={`pos-order-detail-status ${popoverOrder.status}`}>{statusLabel(popoverOrder.status)}</span>
          </header>
          <ul className="pos-order-detail-lines">
            {popoverOrder.lines.map((line, index) => (
              <li key={`${line.productId}-${index}`}>
                <span>{line.name}</span>
                <b>×{line.quantity}</b>
              </li>
            ))}
          </ul>
          <div className="pos-order-detail-total">
            <span>{t('op.pos.cart.total')}</span>
            <b>{formatMinorUnits(popoverOrder.total.minorUnits, popoverOrder.total.currencyCode)}</b>
          </div>
          <div className="pos-order-detail-actions">
            {popoverOrder.status === 'placed' && (
              <button type="button" className="pos-order-detail-accept" onClick={closeAfter(() => void runAction(popoverOrder, 'accept')())}>
                <Check size={14} aria-hidden="true" />{t('op.shopOrders.accept')}
              </button>
            )}
            {popoverOrder.status === 'accepted' && (
              <button type="button" className="pos-order-detail-accept" onClick={closeAfter(() => void runAction(popoverOrder, 'deliver')())}>
                <HandPlatter size={14} aria-hidden="true" />{t('op.shopOrders.deliver')}
              </button>
            )}
            {/* Выданный и отменённый заказ открывается посмотреть: выдавать и отменять в нём нечего. */}
            {canCancel && isOpenStatus(popoverOrder.status) && (
              <button type="button" className="pos-order-detail-cancel" onClick={closeAfter(() => void runAction(popoverOrder, 'cancel')())}>
                {t('op.shopOrders.cancel')}
              </button>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
