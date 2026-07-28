import { Plus } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { formatTime } from '../operatorHelpers';
import type { BookingItem } from './bookingModel';

export function BookingRequestsLane({
  requests, busy, canManage, onCreate, onAccept, onClarify
}: {
  requests: BookingItem[];
  busy: boolean;
  canManage: boolean;
  onCreate: () => void;
  onAccept: (item: BookingItem) => void;
  onClarify: (item: BookingItem) => void;
}) {
  const { t } = useI18n();
  const hasRequests = requests.length > 0;

  // Один ряд: заголовок · карточки заявок · вертикальный разделитель · кнопка «+ Бронь».
  // Карточки и акцентный кант — только при наличии заявок; пустой вид нейтральный (лишь кнопка).
  return (
    <section className={`booking-requests-lane${hasRequests ? '' : ' is-empty'}`} aria-label={t('op.booking.requests.laneTitle')}>
      {hasRequests && (
        <>
          <div className="booking-lane-title"><span>{t('op.booking.requests.laneTitle')}</span></div>
          <div className="booking-lane-cards">
            {requests.map((request) => (
              <article key={request.reservationId} className="booking-lane-card" title={request.note || undefined}>
                <div className="booking-lane-head">
                  <span className="booking-lane-time">
                    {formatTime(new Date(request.startMs).toISOString())}
                    <span className="booking-lane-dash" aria-hidden="true">–</span>
                    {formatTime(new Date(request.endMs).toISOString())}
                  </span>
                  <strong>{request.customerName}</strong>
                </div>
                <div className="booking-lane-actions">
                  <button type="button" disabled={!canManage || busy} onClick={() => onAccept(request)}>{t('op.booking.requests.accept')}</button>
                  <button type="button" onClick={() => onClarify(request)}>{t('op.booking.requests.clarify')}</button>
                </div>
              </article>
            ))}
          </div>
          <span className="booking-lane-sep" aria-hidden="true" />
        </>
      )}
      <button type="button" className="booking-create-action" disabled={!canManage} onClick={onCreate}><Plus size={14} />{t('op.booking.createBtn')}</button>
    </section>
  );
}
