import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { formatCountdown, secondsUntil } from '@/lib/countdown';

/**
 * Сколько у клуба осталось времени ответить на заявку. Не ответит — заявка снимется сама, а
 * замороженные деньги вернутся целиком: истечение срока это не неявка, удерживать не за что.
 */
export function RespondByCountdown({ respondByUtc }: { respondByUtc: string }) {
  const { t } = useI18n();
  const [left, setLeft] = useState(() => secondsUntil(respondByUtc));

  useEffect(() => {
    setLeft(secondsUntil(respondByUtc));
    const id = setInterval(() => setLeft(secondsUntil(respondByUtc)), 1000);
    return () => clearInterval(id);
  }, [respondByUtc]);

  if (left === null) return null;

  if (left === 0) {
    return <p className="mt-2 text-sm text-[var(--text-2)]">{t('customer.reservations.respondOver')}</p>;
  }

  const clock = formatCountdown(left);
  return (
    <div className="mt-2">
      {/* Секундное табло не зачитывается на каждый тик — смысл несёт подпись рядом, а её
          читалка перечитывает только по фокусу. */}
      <p aria-hidden="true" className="text-sm font-bold">
        {t('customer.reservations.respondIn', { clock })}
      </p>
      <span className="sr-only">{`${t('a11y.reservations.respondIn')} ${clock}`}</span>
      <p className="text-sm text-[var(--text-3)]">{t('customer.reservations.respondNote')}</p>
    </div>
  );
}
