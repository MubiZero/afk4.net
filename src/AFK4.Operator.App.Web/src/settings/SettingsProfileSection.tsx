import { useI18n } from '@afk4/i18n';

// Профиль клуба: пара полей (имя/город) + валюта/филиал только для чтения. Состояние живёт
// в родителе (BackendSettingsWorkspace) — его читает боковая readiness-панель, — поэтому секция
// презентационная: получает значения и колбэки, ничего не загружает сама.
export function SettingsProfileSection({
  clubName,
  city,
  currencyCode,
  hasBackend,
  settingsDirty,
  onClubNameChange,
  onCityChange,
  onSave
}: {
  clubName: string;
  city: string;
  currencyCode: string;
  hasBackend: boolean;
  settingsDirty: boolean;
  onClubNameChange: (value: string) => void;
  onCityChange: (value: string) => void;
  onSave: () => void;
}) {
  const { t } = useI18n();
  return (
    <>
      <div className="settings-form-grid">
        <label>{t('op.settings.profile.clubName')}<input value={clubName} onChange={(event) => onClubNameChange(event.currentTarget.value)} /></label>
        <label>{t('op.settings.profile.city')}<input value={city} onChange={(event) => onCityChange(event.currentTarget.value)} /></label>
        <label>{t('op.settings.profile.currency')}<input value={currencyCode} readOnly /></label>
        <label>{t('op.settings.profile.branch')}<input value={hasBackend ? t('op.settings.profile.branchCurrent') : t('op.settings.profile.branchLocal')} readOnly /></label>
      </div>
      <div className="settings-save-row">
        <span>{settingsDirty ? t('op.settings.profile.dirty') : t('op.settings.profile.clean')}</span>
        <button type="button" onClick={onSave}>{t('common.save')}</button>
      </div>
    </>
  );
}
