import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';

/**
 * Отказ в заявке с причиной. Отдельно от «Отменить» намеренно: игрок ничего не отменял, деньги
 * ему возвращаются целиком, и в его сетевые числа отказ не идёт.
 *
 * Причина — код из справочника, а не свободный текст: её читает игрок на своём языке, и по коду
 * же видно, отказывает ли филиал по одной и той же причине третий вечер подряд. Слова
 * администратора идут рядом и обязательны там, где кода нет.
 */
const REASONS: { code: string; key: MessageKey }[] = [
  { code: 'no_seats', key: 'booking.reject.noSeats' },
  { code: 'maintenance', key: 'booking.reject.maintenance' },
  { code: 'event', key: 'booking.reject.event' },
  { code: 'other', key: 'booking.reject.other' }
];

export interface RejectPanelProps {
  busy: boolean;
  onSend: (reasonCode: string, note: string | null) => void;
  onDismiss: () => void;
}

export function RejectPanel({ busy, onSend, onDismiss }: RejectPanelProps) {
  const { t } = useI18n();
  const [reasonCode, setReasonCode] = useState(REASONS[0].code);
  const [note, setNote] = useState('');

  const words = note.trim();
  // «Своими словами» без слов — тот же пустой отказ: игрок снова не узнаёт причину.
  const needsWords = reasonCode === 'other' && words.length === 0;

  return (
    <div className="booking-reject" role="group" aria-label={t('op.booking.reject.title')}>
      <p className="booking-reject-title">{t('op.booking.reject.title')}</p>
      <div className="booking-reject-reasons">
        {REASONS.map((reason) => (
          <label key={reason.code}>
            <input
              type="radio"
              name="reject-reason"
              value={reason.code}
              checked={reasonCode === reason.code}
              onChange={() => setReasonCode(reason.code)}
            />
            <span>{t(reason.key)}</span>
          </label>
        ))}
      </div>
      <label className="booking-field">
        <span>{t('op.booking.reject.note')}</span>
        <input
          type="text"
          value={note}
          maxLength={512}
          onChange={(event) => setNote(event.target.value)}
        />
      </label>
      {needsWords && <p className="booking-reject-hint">{t('op.booking.reject.needWords')}</p>}
      <div className="booking-action-grid">
        <button
          type="button"
          className="danger"
          disabled={busy || needsWords}
          onClick={() => onSend(reasonCode, words.length > 0 ? words : null)}
        >
          {t('op.booking.reject.send')}
        </button>
        <button type="button" disabled={busy} onClick={onDismiss}>
          {t('op.booking.reject.back')}
        </button>
      </div>
    </div>
  );
}
