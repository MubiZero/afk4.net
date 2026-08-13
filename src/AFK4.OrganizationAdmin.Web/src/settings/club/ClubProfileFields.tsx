import type { ReactNode } from 'react';
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
const BRANCH_COVER_PURPOSE = 'branch-cover';

export interface ClubProfileForm {
  name: string;
  city: string;
  description: string;
  address: string;
  phone: string;
  telegram: string;
  website: string;
  instagram: string;
  logoUrl: string | null;
  logoMediaId: string | null;
  coverImageUrl: string | null;
  coverMediaId: string | null;
  // Координаты живут в форме строкой, а не числом: при вводе «38.» число проглотило бы точку
  // и продолжить набор дробной части стало бы невозможно. В число их превращает сборка запроса.
  latitude: string;
  longitude: string;
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
  // «Как видит игрок» кладём между панелями — грид (club-profile-layout) сам ставит его в правый
  // верхний угол, поэтому порядок в DOM не важен.
  preview: ReactNode;
}

// Две панели одинаковой сетки полей (единая ширина колонок = консистентность):
//  1. «Профиль» — лицо игрока + контакты (рядом превью).
//  2. «Часы и настройки» — 7-дневный график + пояс/язык/валюта (во всю ширину под превью).
export function ClubProfileFields({ form, currencyCode, backend, disabled, onField, preview }: ClubProfileFieldsProps) {
  const { t } = useI18n();

  return (
    <>
      <section className="management-panel club-area-profile">
        <div className="mgmt-form">
          <div className="mgmt-section-title"><span>{t('op.club.section.identity')}</span></div>
          <div className="club-identity-grid">
            <label className="club-identity-name">{t('op.settings.profile.clubName')}
              <input value={form.name} disabled={disabled} onChange={(e) => onField('name', e.currentTarget.value)} />
            </label>
            <label className="club-identity-desc">{t('op.club.field.description')}
              <textarea value={form.description} placeholder={t('op.club.ph.description')} disabled={disabled} onChange={(e) => onField('description', e.currentTarget.value)} />
            </label>
            <label className="club-logo-field club-identity-logo">{t('op.club.field.logo')}
              <MediaUpload
                value={form.logoUrl}
                mediaId={form.logoMediaId}
                purpose={BRANCH_LOGO_PURPOSE}
                branchId={backend.branchId}
                backend={backend}
                disabled={disabled}
                onChange={(media) => {
                  onField('logoUrl', media?.url ?? null);
                  onField('logoMediaId', media?.mediaId ?? null);
                }}
              />
            </label>
            {/* Фото зала: в приложении игрок выбирает клуб глазами, и логотип на цветном
                квадрате не говорит ничего о том, как выглядит зал. */}
            <label className="club-logo-field club-identity-cover">{t('op.club.field.cover')}
              <MediaUpload
                value={form.coverImageUrl}
                mediaId={form.coverMediaId}
                purpose={BRANCH_COVER_PURPOSE}
                branchId={backend.branchId}
                backend={backend}
                disabled={disabled}
                onChange={(media) => {
                  onField('coverImageUrl', media?.url ?? null);
                  onField('coverMediaId', media?.mediaId ?? null);
                }}
              />
              <span className="club-field-hint">{t('op.club.hint.cover')}</span>
            </label>
          </div>

          <div className="mgmt-section-title"><span>{t('op.club.section.contacts')}</span></div>
          <div className="club-field-grid">
            <label>{t('op.settings.profile.city')}
              <input value={form.city} placeholder={t('op.club.ph.city')} disabled={disabled} onChange={(e) => onField('city', e.currentTarget.value)} />
            </label>
            <label>{t('op.club.field.address')}
              <input value={form.address} placeholder={t('op.club.ph.address')} disabled={disabled} onChange={(e) => onField('address', e.currentTarget.value)} />
            </label>
            <label>{t('op.club.field.phone')}
              <input value={form.phone} placeholder={t('op.club.ph.phone')} disabled={disabled} onChange={(e) => onField('phone', e.currentTarget.value)} />
            </label>
            <label>{t('op.club.field.telegram')}
              <input value={form.telegram} placeholder={t('op.club.ph.telegram')} disabled={disabled} onChange={(e) => onField('telegram', e.currentTarget.value)} />
            </label>
            <label>{t('op.club.field.website')}
              <input value={form.website} placeholder={t('op.club.ph.website')} disabled={disabled} onChange={(e) => onField('website', e.currentTarget.value)} />
            </label>
            <label>{t('op.club.field.instagram')}
              <input value={form.instagram} placeholder={t('op.club.ph.instagram')} disabled={disabled} onChange={(e) => onField('instagram', e.currentTarget.value)} />
            </label>
            {/* Координаты ставят клуб на карту в приложении. Пустое поле — не ошибка: клуб
                останется в списке, просто без точки на карте. */}
            <label>{t('op.club.field.latitude')}
              <input
                value={form.latitude}
                inputMode="decimal"
                placeholder={t('op.club.ph.latitude')}
                disabled={disabled}
                onChange={(e) => onField('latitude', e.currentTarget.value)}
              />
            </label>
            <label>{t('op.club.field.longitude')}
              <input
                value={form.longitude}
                inputMode="decimal"
                placeholder={t('op.club.ph.longitude')}
                disabled={disabled}
                onChange={(e) => onField('longitude', e.currentTarget.value)}
              />
            </label>
          </div>
          <p className="club-field-hint">{t('op.club.hint.coords')}</p>
        </div>
      </section>

      {preview}

      <section className="management-panel club-area-schedule">
        <div className="mgmt-form">
          <div className="mgmt-section-title"><span>{t('op.club.section.hours')}</span></div>
          <WorkingHoursEditor value={form.workingHours} disabled={disabled} onChange={(days) => onField('workingHours', days)} />

          <div className="mgmt-section-title"><span>{t('op.club.section.settings')}</span></div>
          <div className="club-field-grid">
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
            <label>{t('op.settings.profile.currency')}
              <input value={currencyCode} readOnly />
            </label>
          </div>
        </div>
      </section>
    </>
  );
}
