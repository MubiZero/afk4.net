import { useI18n } from '@afk4/i18n';
import { formatTime } from '../operatorHelpers';
import type { BookingItem } from './bookingModel';

export function BookingRequestsLane({
  requests, busy, canManage, onAccept, onClarify
}: {
  requests: BookingItem[];
  busy: boolean;
  canManage: boolean;
  onAccept: (item: BookingItem) => void;
  onClarify: (item: BookingItem) => void;
}) {
  const { t } = useI18n();
  if (requests.length === 0) return null;

  return (
    <section className="booking-requests-lane" aria-label={t('op.booking.requests.laneTitle')}>
      <header className="booking-lane-title"><span>{t('op.booking.requests.laneTitle')}</span><strong>{requests.length}</strong></header>
      <div className="booking-lane-cards">
        {requests.map((request) => (
          <article key={request.reservationId} className="booking-lane-card">
            <span className="booking-lane-time">{formatTime(new Date(request.startMs).toISOString())}</span>
            <strong>{request.customerName}</strong>
            <em>{request.note || t('op.booking.noComment')}</em>
            <div>
              <button type="button" disabled={!canManage || busy} onClick={() => onAccept(request)}>{t('op.booking.requests.accept')}</button>
              <button type="button" onClick={() => onClarify(request)}>{t('op.booking.requests.clarify')}</button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
