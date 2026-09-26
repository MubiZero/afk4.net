import { useEffect, useState } from 'react';
import { formatDateParts } from '@afk4/formatting';

const systemNow = () => new Date();

export function useMinuteClock(locale: string, nowProvider: () => Date = systemNow): string {
  const [now, setNow] = useState(nowProvider);

  useEffect(() => {
    let timer = window.setTimeout(function tick() {
      setNow(nowProvider());
      timer = window.setTimeout(tick, 60_000);
    }, 60_000 - nowProvider().getTime() % 60_000);

    const refresh = () => {
      if (document.visibilityState === 'visible') {
        setNow(nowProvider());
      }
    };

    document.addEventListener('visibilitychange', refresh);
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', refresh);
    };
  }, [nowProvider]);

  // Через общий форматтер: у таджикского Intl браузера выдал бы «09:40 PM».
  return formatDateParts(now, locale, {
    hour: '2-digit',
    minute: '2-digit'
  });
}
