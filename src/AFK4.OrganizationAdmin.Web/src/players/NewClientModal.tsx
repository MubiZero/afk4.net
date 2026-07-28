import { useI18n } from '@afk4/i18n';
import { UserRoundPlus } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Создание клиента через готовую модалку (вместо вечно-раскрытой формы). Реальное действие
// createPlayer держит оркестратор; здесь — контролируемые поля + сабмит.
export function NewClientModal({
  name,
  phone,
  onChangeName,
  onChangePhone,
  onClose,
  onSubmit,
}: {
  name: string;
  phone: string;
  onChangeName: (value: string) => void;
  onChangePhone: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
}) {
  const { t } = useI18n();
  return (
    <PanelModal title={t('op.players.newClient.title')} subtitle={t('op.players.newClient.subtitle')} onClose={onClose}>
      <form
        className="clients-new-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="new-client-name">{t('op.players.actions.newNameLabel')}</label>
        <input
          id="new-client-name"
          value={name}
          autoFocus
          onChange={(event) => onChangeName(event.currentTarget.value)}
        />
        <label htmlFor="new-client-phone">{t('op.players.actions.newPhoneLabel')}</label>
        <input
          id="new-client-phone"
          value={phone}
          inputMode="tel"
          onChange={(event) => onChangePhone(event.currentTarget.value)}
        />
        <button type="submit" className="clients-primary-action">
          <UserRoundPlus size={15} aria-hidden="true" />
          {t('op.players.newClient.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
