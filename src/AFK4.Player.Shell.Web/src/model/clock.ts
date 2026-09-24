/**
 * Время на экране — время платформы, а не часов ПК.
 *
 * Срок аренды выдаёт сервер, а часы игрового ПК отстают или спешат на минуты: отсчёт по ним
 * показал бы игроку чужие цифры. Агент кладёт в состояние `ObservedAtUtc` — время платформы в
 * момент сборки; разница с часами ПК в момент получения и есть поправка.
 */
export function clockOffsetMs(observedAtUtc: string | null | undefined, receivedAtMs: number): number {
  if (!observedAtUtc) return 0;
  const observed = Date.parse(observedAtUtc);
  return Number.isNaN(observed) ? 0 : observed - receivedAtMs;
}

/** Сколько целых секунд осталось до момента по времени платформы; null — момента нет. */
export function secondsUntil(targetUtc: string | null | undefined, nowMs: number, offsetMs: number): number | null {
  if (!targetUtc) return null;
  const target = Date.parse(targetUtc);
  if (Number.isNaN(target)) return null;
  return Math.max(0, Math.floor((target - (nowMs + offsetMs)) / 1000));
}

/** «1:05:09» или «05:09» — часы появляются, только когда они есть. */
export function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const mm = String(m).padStart(2, '0');
  const ss = String(s).padStart(2, '0');
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`;
}
