import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';

/**
 * Почему клуб отказал — словами, а не кодом. Причина приходит кодом из общего справочника:
 * текст на языке стойки игроку не помог бы, а у кода свой перевод на каждый язык.
 *
 * Судьба денег называется всегда, даже когда причины нет: замороженное при отказе возвращается
 * целиком, и человеку это важнее самой причины.
 */
const REASON_KEYS: Record<string, MessageKey> = {
  no_seats: 'booking.reject.noSeats',
  maintenance: 'booking.reject.maintenance',
  event: 'booking.reject.event'
};

export function Rejection({ reasonCode, note }: { reasonCode: string | null; note: string | null }) {
  const { t } = useI18n();
  const reasonKey = reasonCode ? REASON_KEYS[reasonCode] : undefined;
  const words = note?.trim();

  return (
    <div className="mt-2 space-y-1 text-sm text-[var(--text-2)]">
      {reasonKey && <p>{t(reasonKey)}</p>}
      {words && <p>{words}</p>}
      <p>{t('customer.reservations.rejectMoneyBack')}</p>
    </div>
  );
}
