import { Plus, Timer } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { formatTime } from '../operatorHelpers';
import { useSecondClock } from '../useSecondClock';
import { respondCountdown, type BookingItem } from './bookingModel';

// Порог «горит»: последняя минута обещания. Дальше карточка меняет тон, чтобы заявка не
// досиживала свой срок незамеченной в углу экрана.
const URGENT_MS = 60_000;

export function BookingRequestsLane({
  requests, busy, canManage, onCreate, onAccept, onClarify, nowProvider
}: {
  requests: BookingItem[];
  busy: boolean;
  canManage: boolean;
  onCreate: () => void;
  onAccept: (item: BookingItem) => void;
  onClarify: (item: BookingItem) => void;
  nowProvider?: () => number;
}) {
  const { t } = useI18n();
  const hasRequests = requests.length > 0;
  // Секундные часы заводим, только когда есть кому тикать.
  const hasDeadline = requests.some((request) => request.respondByMs !== null);
  const nowMs = useSecondClock(hasDeadline, nowProvider);

  // Один ряд: заголовок · карточки заявок · вертикальный разделитель · кнопка «+ Бронь».
  // Карточки и акцентный кант — только при наличии заявок; пустой вид нейтральный (лишь кнопка).
  return (
    <section className={`booking-requests-lane${hasRequests ? '' : ' is-empty'}`} aria-label={t('op.booking.requests.laneTitle')}>
      {hasRequests && (
        <>
          <div className="booking-lane-title"><span>{t('op.booking.requests.laneTitle')}</span></div>
          <div className="booking-lane-cards">
            {requests.map((request) => {
              const countdown = respondCountdown(request, nowMs);
              const urgent = countdown !== null && !countdown.overdue
                && request.respondByMs !== null && request.respondByMs - nowMs <= URGENT_MS;
              return (
                <article
                  key={request.reservationId}
                  className={`booking-lane-card${countdown?.overdue ? ' is-overdue' : urgent ? ' is-urgent' : ''}`}
                  title={request.note || undefined}
                >
                  <div className="booking-lane-head">
                    <span className="booking-lane-time">
                      {formatTime(new Date(request.startMs).toISOString())}
                      <span className="booking-lane-dash" aria-hidden="true">–</span>
                      {formatTime(new Date(request.endMs).toISOString())}
                    </span>
                    <strong>{request.customerName}</strong>
                  </div>
                  {countdown !== null && (
                    <div className="booking-lane-respond">
                      <Timer size={12} aria-hidden="true" />
                      <span>
                        {countdown.overdue
                          ? t('op.booking.requests.respondOverdue')
                          : t('op.booking.requests.respondIn', { time: countdown.label })}
                      </span>
                    </div>
                  )}
                  <div className="booking-lane-actions">
                    <button type="button" disabled={!canManage || busy} onClick={() => onAccept(request)}>{t('op.booking.requests.accept')}</button>
                    <button type="button" onClick={() => onClarify(request)}>{t('op.booking.requests.clarify')}</button>
                  </div>
                </article>
              );
            })}
          </div>
          <span className="booking-lane-sep" aria-hidden="true" />
        </>
      )}
      <button type="button" className="booking-create-action" disabled={!canManage} onClick={onCreate}><Plus size={14} />{t('op.booking.createBtn')}</button>
    </section>
  );
}
