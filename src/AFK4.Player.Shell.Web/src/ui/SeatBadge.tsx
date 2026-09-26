import { useI18n } from '@afk4/i18n';

interface SeatBadgeProps {
  seatLabel: string | null | undefined;
  zoneName: string | null | undefined;
  free?: boolean;
}

/**
 * «ПК 07 · Свободен» — первое, что читается на экране, и видно от стойки. Номер места клуб
 * набирает сам; если места нет, знак молчит, а не показывает имя машины в сети.
 */
export function SeatBadge({ seatLabel, zoneName, free }: SeatBadgeProps) {
  const { t } = useI18n();
  if (!seatLabel) return null;
  return (
    <div className="seat-badge">
      <span className="seat-badge__label mono">{seatLabel}</span>
      {free ? (
        <span className="seat-badge__state">
          <span className="seat-badge__dot" aria-hidden="true" />
          {t('playerShell.seat.free')}
        </span>
      ) : null}
      {zoneName ? <span className="seat-badge__zone">{zoneName}</span> : null}
    </div>
  );
}
