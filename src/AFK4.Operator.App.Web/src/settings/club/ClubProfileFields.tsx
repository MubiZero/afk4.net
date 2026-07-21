import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { MediaUpload } from '../../components/MediaUpload';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WorkingHoursEditor } from './WorkingHoursEditor';

// Media purpose used by the branch-logo upload. The frontend has no `@afk4/contracts` package
// (that name is a C# assembly, `AFK4.Shared.Contracts`) — `MediaUpload`'s `purpose` prop is a
// plain string, and the existing MediaUpload.test.tsx already uses this same literal.
const BRANCH_LOGO_PURPOSE = 'branch-logo';

export interface ClubProfileForm {
  name: string;
  city: string;
  description: string;
  address: string;
  phone: string;
  telegram: string;
  website: string;
  logoUrl: string | null;
  logoMediaId: string | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDay[];
}

const TIME_ZONES = ['Asia/Dushanbe', 'Asia/Tashkent', 'Asia/Almaty', 'Asia/Bishkek', 'Europe/Moscow', 'Asia/Yekaterinburg'];
const LOCALES: Array<{ value: string; key: MessageKey }> = [
  { value: 'ru', key: 'op.club.locale.ru' },
  { value: 'tg', key: 'op.club.locale.tg' },
  { value: 'en', key: 'op.club.locale.en' }
];

interface ClubProfileFieldsProps {
  form: ClubProfileForm;
  currencyCode: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
  onField: <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => void;
}

export function ClubProfileFields({ form, currencyCode, backend, disabled, onField }: ClubProfileFieldsProps) {
  const { t } = useI18n();

  return (
    <div className="mgmt-form">
      <div className="mgmt-section-title"><span>{t('op.club.section.identity')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.settings.profile.clubName')}
          <input value={form.name} disabled={disabled} onChange={(e) => onField('name', e.currentTarget.value)} />
        </label>
        <label className="mgmt-form-wide">{t('op.club.field.description')}
          <input value={form.description} disabled={disabled} onChange={(e) => onField('description', e.currentTarget.value)} />
        </label>
      </div>
      <label className="club-logo-field">{t('op.club.field.logo')}
        <MediaUpload
          value={form.logoUrl}
          purpose={BRANCH_LOGO_PURPOSE}
          branchId={backend.branchId}
          backend={backend}
          disabled={disabled}
          onChange={(media) => {
            onField('logoUrl', media?.url ?? null);
            onField('logoMediaId', media?.mediaId ?? null);
          }}
        />
        <span className="media-upload-hint">{t('op.club.logo.hint')}</span>
      </label>

      <div className="mgmt-section-title"><span>{t('op.club.section.contacts')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.club.field.address')}
          <input value={form.address} disabled={disabled} onChange={(e) => onField('address', e.currentTarget.value)} />
        </label>
        <label>{t('op.settings.profile.city')}
          <input value={form.city} disabled={disabled} onChange={(e) => onField('city', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.phone')}
          <input value={form.phone} disabled={disabled} onChange={(e) => onField('phone', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.telegram')}
          <input value={form.telegram} disabled={disabled} onChange={(e) => onField('telegram', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.website')}
          <input value={form.website} disabled={disabled} onChange={(e) => onField('website', e.currentTarget.value)} />
        </label>
      </div>

      <div className="mgmt-section-title"><span>{t('op.club.section.hours')}</span></div>
      <WorkingHoursEditor value={form.workingHours} disabled={disabled} onChange={(days) => onField('workingHours', days)} />

      <div className="mgmt-section-title"><span>{t('op.club.section.settings')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.club.field.timezone')}
          <select value={form.timeZone} disabled={disabled} onChange={(e) => onField('timeZone', e.currentTarget.value)}>
            {TIME_ZONES.map((tz) => <option key={tz} value={tz}>{tz}</option>)}
          </select>
        </label>
        <label>{t('op.club.field.locale')}
          <select value={form.locale} disabled={disabled} onChange={(e) => onField('locale', e.currentTarget.value)}>
            {LOCALES.map((l) => <option key={l.value} value={l.value}>{t(l.key)}</option>)}
          </select>
        </label>
      </div>
      <div className="mgmt-meta-grid">
        <div className="mgmt-meta-row">
          <span className="mgmt-meta-label">{t('op.settings.profile.currency')}</span>
          <span className="mgmt-meta-value">{currencyCode}</span>
        </div>
      </div>
    </div>
  );
}
