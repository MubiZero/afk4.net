import { clockOffsetMs, formatDuration, secondsUntil } from '../model/clock';
import { useClock } from './useSecondTick';

interface CountdownProps {
  /** Конец по времени платформы. */
  untilUtc: string | null | undefined;
  /** Время платформы в момент сборки состояния и когда оно пришло — для поправки часов ПК. */
  observedAtUtc: string | null | undefined;
  receivedAtMs: number | null;
  className?: string;
}

/** Сколько осталось — моноширинными цифрами, чтобы строка не прыгала на каждой секунде. */
export function Countdown({ untilUtc, observedAtUtc, receivedAtMs, className }: CountdownProps) {
  const now = useClock('second');
  const offset = clockOffsetMs(observedAtUtc, receivedAtMs ?? now);
  const seconds = secondsUntil(untilUtc, now, offset);
  if (seconds === null) return null;
  return (
    <span className={['mono', className].filter(Boolean).join(' ')} data-testid="countdown">
      {formatDuration(seconds)}
    </span>
  );
}
