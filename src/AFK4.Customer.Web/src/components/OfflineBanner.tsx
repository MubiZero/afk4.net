import { useEffect, useState } from 'react';

// `online` is injectable for tests; in the app it tracks navigator.onLine + online/offline events.
export function OfflineBanner({ online }: { online?: boolean }) {
  const [isOnline, setIsOnline] = useState(online ?? (typeof navigator === 'undefined' ? true : navigator.onLine));

  useEffect(() => {
    if (online !== undefined) { setIsOnline(online); return; }
    const update = () => setIsOnline(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, [online]);

  if (isOnline) return null;
  return (
    <div role="status" className="bg-[var(--color-surface-2)] px-4 py-2 text-center text-xs text-[var(--text-2)]">
      Офлайн — показаны сохранённые данные
    </div>
  );
}
