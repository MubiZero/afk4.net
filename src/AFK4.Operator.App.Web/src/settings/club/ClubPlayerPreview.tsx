import { useI18n } from '@afk4/i18n';
import type { ClubProfileForm } from './ClubProfileFields';

// «Как видит игрок»: управление вниманием — оператор сразу видит эффект правок.
export function ClubPlayerPreview({ form }: { form: ClubProfileForm }) {
  const { t } = useI18n();
  // JS getDay(): 0=Вс..6=Сб → в ISO 1=Пн..7=Вс. Без Date.now() в тестах: берём new Date() в рантайме UI.
  const isoToday = ((new Date().getDay() + 6) % 7) + 1;
  const today = form.workingHours.find((d) => d.dayOfWeek === isoToday);

  return (
    <aside className="club-preview">
      <div className="mgmt-section-title"><span>{t('op.club.section.preview')}</span></div>
      <div className="club-preview-card">
        {form.logoUrl
          ? <img className="club-preview-logo" src={form.logoUrl} alt="" />
          : <div className="club-preview-logo club-preview-logo--empty">{t('op.club.preview.noLogo')}</div>}
        <div className="club-preview-name">{form.name}</div>
        {form.description && <div className="club-preview-desc">{form.description}</div>}
        {(form.address || form.city) && (
          <div className="club-preview-address">{[form.address, form.city].filter(Boolean).join(', ')}</div>
        )}
        <div className="club-preview-today">
          {today?.isClosed || !today
            ? t('op.club.preview.closedToday')
            : `${t('op.club.preview.today')}: ${today.openTime}–${today.closeTime}`}
        </div>
        {form.phone && <div className="club-preview-contact">{form.phone}</div>}
        {form.telegram && <div className="club-preview-contact">{form.telegram}</div>}
      </div>
      <p className="club-preview-hint">{t('op.club.preview.hint')}</p>
    </aside>
  );
}
