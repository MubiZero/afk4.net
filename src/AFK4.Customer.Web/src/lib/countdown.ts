/** Сколько секунд осталось до момента. `null` — срока нет или он нечитаем; ноль — срок вышел. */
export function secondsUntil(deadlineIso: string | null | undefined, now: Date = new Date()): number | null {
  if (!deadlineIso) return null;
  const deadline = Date.parse(deadlineIso);
  if (Number.isNaN(deadline)) return null;
  return Math.max(0, Math.ceil((deadline - now.getTime()) / 1000));
}

/** «12:30», а при сроке длиннее часа — «1:02:05». Часы не дописываются впустую: у пятнадцати
 *  минут ожидания ведущий ноль часа только мешает читать. */
export function formatCountdown(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const mm = String(Math.floor((seconds % 3600) / 60)).padStart(2, '0');
  const ss = String(seconds % 60).padStart(2, '0');
  return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
}
