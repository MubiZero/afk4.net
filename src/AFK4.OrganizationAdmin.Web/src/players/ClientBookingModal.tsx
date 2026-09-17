import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import { PanelModal } from '../PanelModal';

export interface ClientBookingDraft {
  startsAt: string;
  durationMinutes: number;
  seatId: string;
}

/**
 * Бронь для клиента прямо из его карточки.
 *
 * Раньше кнопка «Забронировать» создавала бронь сразу по клику: всегда «через 30 минут», всегда
 * на час, всегда без места. Оператор не видел и не выбирал ничего, а потом шёл в «Брони» искать
 * эту запись и назначать ей место. Одна кнопка — и необратимая запись, которой никто не заказывал.
 */
export function ClientBookingModal({ clientName, draft, seats, busy, onChange, onClose, onSubmit }: {
  clientName: string;
  draft: ClientBookingDraft;
  seats: { seatId: string; label: string }[];
  busy: boolean;
  onChange: (draft: ClientBookingDraft) => void;
  onClose: () => void;
  onSubmit: () => void;
}) {
  const { t } = useI18n();

  return (
    <PanelModal
      title={t('op.players.booking.title')}
      subtitle={t('op.players.booking.subtitle', { name: clientName })}
      onClose={onClose}
      closeDisabled={busy}
    >
      <form
        className="clients-new-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="client-booking-start">{t('op.booking.create.start')}</label>
        <input
          id="client-booking-start"
          type="datetime-local"
          value={draft.startsAt}
          autoFocus
          disabled={busy}
          onChange={(event) => onChange({ ...draft, startsAt: event.currentTarget.value })}
        />
        <label htmlFor="client-booking-duration">{t('op.booking.create.duration')}</label>
        <input
          id="client-booking-duration"
          type="number"
          min="15"
          step="15"
          value={String(draft.durationMinutes)}
          disabled={busy}
          onChange={(event) => onChange({ ...draft, durationMinutes: Math.max(15, Number(event.currentTarget.value) || 60) })}
        />
        <label htmlFor="client-booking-seat">{t('op.booking.create.seat')}</label>
        <select
          id="client-booking-seat"
          value={draft.seatId}
          disabled={busy}
          onChange={(event) => onChange({ ...draft, seatId: event.currentTarget.value })}
        >
          {/* Место можно и не выбирать: телефонную заявку часто записывают «на любой свободный».
              Но тогда это осознанный выбор, а не единственный вариант, как было раньше. */}
          <option value="">{t('op.players.booking.seatLater')}</option>
          {seats.map((seat) => <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>)}
        </select>
        <div className="mgmt-form-actions">
          <button type="button" className="ui-btn" disabled={busy} onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || draft.startsAt === ''}>
            <CalendarClock size={14} aria-hidden="true" />
            {t('op.booking.create.submit')}
          </button>
        </div>
      </form>
    </PanelModal>
  );
}
