import { useEffect, useState } from 'react';

/**
 * Текущее время, обновляемое по границе секунды — или минуты. Показ страницы после скрытия
 * обновляет его сразу.
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

    // Скрытая страница не тикает: оболочка прячется и засыпает, пока впереди игра, а Chromium
    // растягивает таймеры невидимой страницы до минуты и дольше. Вернулась — время берётся из часов
    // сразу, а не когда дойдёт запоздавший таймер: иначе игрок видел бы остаток, каким он был в
    // момент ухода (58:07 при реальных 53).
    const onVisibility = () => {
      if (document.visibilityState !== 'visible') return;
      clearTimeout(timer);
      setNow(Date.now());
      schedule();
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => {
      document.removeEventListener('visibilitychange', onVisibility);
      clearTimeout(timer);
    };
  }, [unit]);

  return now;
}
