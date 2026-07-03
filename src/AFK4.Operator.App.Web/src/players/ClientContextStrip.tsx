import { useI18n } from '@afk4/i18n';
import { CalendarClock, MonitorPlay } from 'lucide-react';
import type { ClientLiveContext } from './playersModel';

// Кросс-контекст профиля: «играет сейчас на РС-XX» и «ближайшая бронь на HH:MM».
// Компактная мета-строка внутри карточки-личности (не отдельная полоса пилюль):
// иконка + приглушённый текст в стиле проекта. «Играет» подсвечен акцентом (живое),
// «бронь» — вторичным. Чисто аддитивна: если ни сессии, ни брони нет — ничего не рисуем
// (отсутствие лучше полу-наличия). Состояния симметричны, каждая часть появляется сама.
export function ClientContextStrip({ context }: { context: ClientLiveContext }) {
  const { t } = useI18n();

  if (context.session === null && context.nextBooking === null) {
    return null;
  }

  return (
    <div className="client-context-meta">
      {context.session !== null && (
        <span className="client-context-item is-live">
          <MonitorPlay size={13} aria-hidden="true" />
          {t('op.players.context.playingOn', { seat: context.session.seatName })}
          {' · '}
          {context.session.untilLabel
            ? t('op.players.context.until', { time: context.session.untilLabel })
            : t('op.players.context.openTab')}
        </span>
      )}
      {context.nextBooking !== null && (
        <span className="client-context-item">
          <CalendarClock size={13} aria-hidden="true" />
          {t('op.players.context.nextBooking', { time: context.nextBooking.timeLabel })}
          {context.nextBooking.seatName ? ` · ${context.nextBooking.seatName}` : ''}
        </span>
      )}
    </div>
  );
}
