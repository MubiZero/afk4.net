import { useI18n } from '@afk4/i18n';
import { Save } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Правка профиля клиента (имя/телефон). Презентационный: реальный вызов updateProfile —
// в оркестраторе. Submit заблокирован при пустом имени (имя обязательно, зеркало бэка).
export function EditProfileModal({
  name,
  phone,
  onChangeName,
  onChangePhone,
  onClose,
  onSubmit,
  busy,
}: {
  name: string;
  phone: string;
  onChangeName: (value: string) => void;
  onChangePhone: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const nameEmpty = name.trim().length === 0;

  return (
    <PanelModal
      title={t('op.players.editProfile.title')}
      subtitle={t('op.players.editProfile.subtitle')}
      onClose={onClose}
    >
      <form
        className="clients-edit-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="edit-client-name">{t('op.players.editProfile.nameLabel')}</label>
        <input
          id="edit-client-name"
          value={name}
          autoFocus
          disabled={busy}
          onChange={(event) => onChangeName(event.currentTarget.value)}
        />
        <label htmlFor="edit-client-phone">{t('op.players.editProfile.phoneLabel')}</label>
        <input
          id="edit-client-phone"
          value={phone}
          inputMode="tel"
          disabled={busy}
          onChange={(event) => onChangePhone(event.currentTarget.value)}
        />
        <button type="submit" className="clients-primary-action" disabled={busy || nameEmpty}>
          <Save size={15} aria-hidden="true" />
          {t('op.players.editProfile.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
