import type { CategoryOption } from './categoryModel';

/**
 * Новый порядок категорий после перестановки одной на шаг вверх или вниз.
 *
 * Возвращает весь список идентификаторов — ровно то, что ждёт сервер: порядок есть свойство
 * набора, и присланный целиком он не оставляет места расхождению. `null` значит «двигать некуда»
 * (край списка или неизвестная категория) — вызывающему нечего отправлять.
 */
export function moveCategory(
  categories: readonly CategoryOption[],
  categoryId: string,
  direction: -1 | 1
): string[] | null {
  const index = categories.findIndex((category) => category.categoryId === categoryId);
  if (index < 0) return null;

  const target = index + direction;
  if (target < 0 || target >= categories.length) return null;

  const order = categories.map((category) => category.categoryId);
  [order[index], order[target]] = [order[target], order[index]];
  return order;
}
