import { useI18n } from '@afk4/i18n';
import { KeyRound } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Установка/сброс PIN клиента (вход на игровое место). Презентационный; валидация ≥4 — зеркало бэка.
export function PinModal({
  pin,
  onChangePin,
  onClose,
  onSubmit,
  busy,
}: {
  pin: string;
  onChangePin: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const tooShort = pin.trim().length < 4;

  return (
    <PanelModal title={t('op.players.pin.title')} subtitle={t('op.players.pin.subtitle')} onClose={onClose}>
      <form
        className="clients-pin-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="client-pin">{t('op.players.pin.label')}</label>
        <input
          id="client-pin"
          inputMode="numeric"
          autoFocus
          value={pin}
          disabled={busy}
          onChange={(event) => onChangePin(event.currentTarget.value)}
        />
        <span className="clients-pin-hint">{t('op.players.pin.hint')}</span>

        <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || tooShort}>
          <KeyRound size={15} aria-hidden="true" />
          {t('op.players.pin.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
