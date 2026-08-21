import { useEffect, useState } from 'react';

/**
 * Текущее время в миллисекундах, обновляемое раз в секунду, пока `active`.
 *
 * Отдельно от useMinuteClock: часы на стене меняются раз в минуту, а обратный отсчёт «ответить
 * за 4:12» — раз в секунду, и минутной точности ему мало. Когда считать нечего (`active`
 * выключен) таймер не заводится вовсе: пустая полоса заявок не должна будить рендер каждую
 * секунду весь вечер.
 */
export function useSecondClock(active: boolean, nowProvider: () => number = Date.now): number {
  const [now, setNow] = useState(nowProvider);

  useEffect(() => {
    if (!active) return undefined;
    setNow(nowProvider());
    const timer = window.setInterval(() => setNow(nowProvider()), 1000);
    return () => window.clearInterval(timer);
  }, [active, nowProvider]);

  return now;
}
