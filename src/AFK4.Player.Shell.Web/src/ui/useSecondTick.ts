import { useEffect, useState } from 'react';

/**
 * Текущее время, обновляемое по границе секунды — или минуты.
 *
 * Не `setInterval(…, 1000)`: интервал дрейфует от границы секунды, и отсчёт то застывает на
 * одной цифре на две секунды, то перепрыгивает. Тикает только тот компонент, которому нужно
 * время: остальной экран между тиками не перерисовывается.
 */
export function useClock(unit: 'second' | 'minute' = 'second'): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const step = unit === 'second' ? 1000 : 60_000;
    let timer: ReturnType<typeof setTimeout>;
    const schedule = () => {
      const current = Date.now();
      timer = setTimeout(() => {
        setNow(Date.now());
        schedule();
      }, step - (current % step) + 5);
    };
    schedule();
    return () => clearTimeout(timer);
  }, [unit]);

  return now;
}
