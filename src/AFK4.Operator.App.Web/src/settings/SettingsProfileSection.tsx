import { useI18n } from '@afk4/i18n';

// Профиль клуба: пара полей (имя/город) + валюта/филиал только для чтения. Состояние живёт
// в родителе — его читает боковая readiness-панель (BackendSettingsWorkspace) или единая
// save-bar сценария Управление (ClubDestination), — поэтому секция презентационная: получает
// значения и колбэки, ничего не загружает и не сохраняет сама. `settingsDirty`/`onSave` остаются
// в контракте ради BackendSettingsWorkspace (его собственная readiness-строка вне этой секции),
// но внутри секции больше не дублируют save-бар — Управление рисует ровно одну кнопку «Сохранить»
// (ManagementScreen), а не две на один экран.
export function SettingsProfileSection({
  clubName,
  city,
  currencyCode,
  hasBackend,
  onClubNameChange,
  onCityChange
}: {
  clubName: string;
  city: string;
  currencyCode: string;
  hasBackend: boolean;
  settingsDirty?: boolean;
  onClubNameChange: (value: string) => void;
  onCityChange: (value: string) => void;
  onSave?: () => void;
}) {
  const { t } = useI18n();
  return (
    <div className="settings-form-grid">
      <label>{t('op.settings.profile.clubName')}<input value={clubName} onChange={(event) => onClubNameChange(event.currentTarget.value)} /></label>
      <label>{t('op.settings.profile.city')}<input value={city} onChange={(event) => onCityChange(event.currentTarget.value)} /></label>
      <label>{t('op.settings.profile.currency')}<input value={currencyCode} readOnly /></label>
      <label>{t('op.settings.profile.branch')}<input value={hasBackend ? t('op.settings.profile.branchCurrent') : t('op.settings.profile.branchLocal')} readOnly /></label>
    </div>
  );
}
