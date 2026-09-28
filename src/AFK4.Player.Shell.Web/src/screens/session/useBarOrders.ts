import { useCallback, useEffect, useState } from 'react';
import type { ShopOrderDto } from '@afk4/contracts';
import { getJson } from '../../api/playerApi';
import { isOrderActive } from '../../model/bar';

/** Как часто спрашивать, где заказ: стойка отвечает за минуты, а не за секунды. */
const ORDER_POLL_MS = 10_000;

/**
 * Заказы бара — на весь экран сессии, а не на вкладку: ушёл на «Игры» — статус заказа всё равно
 * виден в колонке и продолжает обновляться. Пока заказа в работе нет, опроса нет.
 */
export function useBarOrders(baseUrl: string | null, enabled: boolean) {
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);

  const reload = useCallback(async () => {
    if (!baseUrl) return;
    try {
      const list = await getJson<ShopOrderDto[]>(baseUrl, '/api/me/shop/orders');
      if (Array.isArray(list)) setOrders(list);
    } catch {
      // Статус заказа подтянется на следующем круге; меню от этого не ломается.
    }
  }, [baseUrl]);

  useEffect(() => {
    if (enabled) void reload();
  }, [enabled, reload]);

  const hasActive = orders.some(isOrderActive);
  useEffect(() => {
    if (!enabled || !hasActive) return undefined;
    const timer = window.setInterval(() => void reload(), ORDER_POLL_MS);
    return () => window.clearInterval(timer);
  }, [enabled, hasActive, reload]);

  // Ответ сервера на заказ или отмену: заменить на месте, новый — первым.
  const apply = useCallback((order: ShopOrderDto) => {
    setOrders((current) =>
      current.some((existing) => existing.id === order.id)
        ? current.map((existing) => (existing.id === order.id ? order : existing))
        : [order, ...current]);
  }, []);

  return { orders, reload, apply };
}
