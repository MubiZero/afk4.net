import { Timer } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@afk4/ui/react';
import { formatTime } from '../operatorHelpers';
import { formatLocal } from '../phoneFormat';
import { useSecondClock } from '../useSecondClock';
import { respondCountdown, type BookingItem } from './bookingModel';

// Порог «горит»: последняя минута обещания. Дальше карточка меняет тон, чтобы заявка не
// досиживала свой срок незамеченной в углу экрана.
const URGENT_MS = 60_000;

export function BookingRequestsLane({
  requests, busy, canManage, onAccept, onClarify, nowProvider
}: {
  requests: BookingItem[];
  busy: boolean;
  canManage: boolean;
  onAccept: (item: BookingItem) => void;
  onClarify: (item: BookingItem) => void;
  nowProvider?: () => number;
}) {
  const { t } = useI18n();
  const hasRequests = requests.length > 0;
  // Секундные часы заводим, только когда есть кому тикать.
  const hasDeadline = requests.some((request) => request.respondByMs !== null);
  const nowMs = useSecondClock(hasDeadline, nowProvider);

  // Нет заявок — нет и полосы: «Добавить бронь» живёт в шапке раздела, и пустая рамка над лентой
  // только отнимала высоту у ганта.
  if (!hasRequests) return null;

  return (
    <section className="booking-requests-lane" aria-label={t('op.booking.requests.laneTitle')}>
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
              {/* Номер видимой строкой, а не только подсказкой при наведении: по заявке
                  перезванивают, и если у брони есть заметка, она вытесняла номер из
                  подсказки целиком. */}
              {request.phoneNumber && (
                <span className="booking-lane-phone">+992 {formatLocal(request.phoneNumber)}</span>
              )}
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
              {/* «Принять» — та же главная кнопка, что в карточке брони: одно действие, один вид. */}
              <div className="booking-lane-actions">
                {canManage && <Button variant="primary" size="sm" disabled={busy} onClick={() => onAccept(request)}>{t('op.booking.requests.accept')}</Button>}
                <Button size="sm" onClick={() => onClarify(request)}>{t('op.booking.requests.clarify')}</Button>
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}
