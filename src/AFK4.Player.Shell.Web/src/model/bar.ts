import { ShopOrderStatusNames, type ShopCatalogItemDto, type ShopOrderDto } from '@afk4/contracts';
import type { MessageKey } from '@afk4/i18n';
import { PlayerApiError } from '../api/playerApi';

/** Корзина — количество по товару. Ноль из корзины убирается: пустая строка ничего не заказывает. */
export type Cart = Record<string, number>;

/** Предел одной позиции: больше двадцати к месту — скорее опечатка, чем заказ. */
export const MAX_PER_ITEM = 20;

/**
 * Остаток на складе здесь не режет: ноль значит «не считают», а товар, который продают в минус,
 * остатком не ограничен. Хватит ли — решает сервер (`out_of_stock`), страница только предупреждает.
 */
export function changeQuantity(cart: Cart, productId: string, delta: number): Cart {
  const next = Math.min(MAX_PER_ITEM, Math.max(0, (cart[productId] ?? 0) + delta));
  const { [productId]: _removed, ...rest } = cart;
  return next === 0 ? rest : { ...rest, [productId]: next };
}

/** «Осталось 2 штуки» — только когда их правда мало. */
export function fewLeft(item: ShopCatalogItemDto): boolean {
  return item.stockOnHand > 0 && item.stockOnHand <= 3;
}

export function cartTotalMinor(cart: Cart, catalog: ShopCatalogItemDto[]): number {
  return catalog.reduce((sum, item) => sum + (cart[item.productId] ?? 0) * item.price.minorUnits, 0);
}

export function cartLines(cart: Cart): { productId: string; quantity: number }[] {
  return Object.entries(cart).map(([productId, quantity]) => ({ productId, quantity }));
}

/** Заказ, за которым есть смысл следить: его готовят или несут. */
export function isOrderActive(order: ShopOrderDto): boolean {
  return order.status === ShopOrderStatusNames.Placed || order.status === ShopOrderStatusNames.Accepted;
}

export const ORDER_STATUS_KEYS: Record<string, MessageKey> = {
  [ShopOrderStatusNames.Placed]: 'playerShell.bar.status.placed',
  [ShopOrderStatusNames.Accepted]: 'playerShell.bar.status.accepted',
  [ShopOrderStatusNames.Delivered]: 'playerShell.bar.status.delivered',
  [ShopOrderStatusNames.Cancelled]: 'playerShell.bar.status.cancelled'
};

/** Отказ заказа — по коду сервера, теми же словами, что в приложении. */
export function barErrorKey(reason: unknown): MessageKey {
  if (!(reason instanceof PlayerApiError)) return 'playerShell.chooseTime.error.offline';
  switch (reason.code) {
    case 'insufficient_funds':
      return 'playerShell.bar.error.funds';
    case 'out_of_stock':
      return 'playerShell.bar.error.stock';
    case 'product_unavailable':
      return 'playerShell.bar.error.unavailable';
    case 'placement_context_invalid':
    case 'no_active_session':
      return 'playerShell.bar.error.noSession';
    case 'open_shift_required':
      return 'playerShell.bar.error.counterClosed';
    default:
      return 'playerShell.bar.error.generic';
  }
}
