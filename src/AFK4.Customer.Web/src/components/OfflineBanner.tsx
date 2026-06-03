import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';

// `online` is injectable for tests; in the app it tracks navigator.onLine + online/offline events.
export function OfflineBanner({ online }: { online?: boolean }) {
  const { t } = useI18n();
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
      {t('customer.offline.banner')}
    </div>
  );
}
