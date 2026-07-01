import { useI18n } from '@afk4/i18n';
import { CalendarClock, MonitorPlay } from 'lucide-react';
import type { ClientLiveContext } from './playersModel';

// Кросс-контекст профиля: «играет сейчас на РС-XX» и «ближайшая бронь на HH:MM».
// Чисто аддитивная полоса — если ни сессии, ни брони нет, не рисуем ничего
// (отсутствие лучше полу-наличия). Состояния симметричны: каждая часть появляется
// сама по себе, не оставляя «дырки».
export function ClientContextStrip({ context }: { context: ClientLiveContext }) {
  const { t } = useI18n();

  if (context.session === null && context.nextBooking === null) {
    return null;
  }

  return (
    <div className="client-context-strip">
      {context.session !== null && (
        <span className="ui-chip ui-chip--status is-live">
          <MonitorPlay size={14} aria-hidden="true" />
          <span>
            {t('op.players.context.playingOn', { seat: context.session.seatName })}
            {' · '}
            {context.session.untilLabel
              ? t('op.players.context.until', { time: context.session.untilLabel })
              : t('op.players.context.openTab')}
          </span>
        </span>
      )}
      {context.nextBooking !== null && (
        <span className="ui-chip ui-chip--status is-booking">
          <CalendarClock size={14} aria-hidden="true" />
          <span>
            {t('op.players.context.nextBooking', { time: context.nextBooking.timeLabel })}
            {context.nextBooking.seatName ? ` · ${context.nextBooking.seatName}` : ''}
          </span>
        </span>
      )}
    </div>
  );
}
