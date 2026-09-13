/**
 * Зеркало серверного правила PosSelfVoidPolicy на клиенте: показывать ли кассиру отмену его
 * собственного только что пробитого чека.
 *
 * Зеркало, а не источник правды: решает сервер. Клиент повторяет правило ровно затем, чтобы не
 * рисовать кнопку, которая гарантированно ответит отказом — это ровно тот класс дефекта, из-за
 * которого лента заказов бара показывалась кассиру и падала на каждом действии.
 *
 * Любое расхождение допустимо только в одну сторону: клиент может спрятать то, что сервер бы
 * разрешил (человек позовёт старшего), но не может показать то, что сервер запретит.
 */

/// Те же пять минут, что в PosSelfVoidPolicy.SelfVoidWindow.
export const SELF_VOID_WINDOW_MS = 5 * 60 * 1000;

export function canSelfVoidSale(input: {
  createdByStaffUserId: string;
  createdAtUtc: string;
  saleShiftId: string;
  actorStaffUserId: string;
  openShiftId: string | null;
  nowMs: number;
}): boolean {
  if (input.createdByStaffUserId !== input.actorStaffUserId) return false;
  if (input.openShiftId === null || input.saleShiftId !== input.openShiftId) return false;

  const createdMs = Date.parse(input.createdAtUtc);
  if (Number.isNaN(createdMs)) return false;

  const age = input.nowMs - createdMs;
  return age >= 0 && age <= SELF_VOID_WINDOW_MS;
}
