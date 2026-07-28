import { useEffect, useState } from 'react';

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

  return new Intl.DateTimeFormat(locale, {
    hour: '2-digit',
    minute: '2-digit'
  }).format(now);
}
