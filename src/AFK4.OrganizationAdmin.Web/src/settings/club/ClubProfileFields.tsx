import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { MediaUpload } from '../../components/MediaUpload';
import { GalleryUpload, type GalleryPhoto } from '../../components/GalleryUpload';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WorkingHoursEditor } from './WorkingHoursEditor';
import { SkeletonControl, SkeletonLine } from '../../LoadingSkeleton';

// Media purpose used by the branch-logo upload. The frontend has no `@afk4/contracts` package
// (that name is a C# assembly, `AFK4.Shared.Contracts`) — `MediaUpload`'s `purpose` prop is a
// plain string, and the existing MediaUpload.test.tsx already uses this same literal.
const BRANCH_LOGO_PURPOSE = 'branch-logo';
const BRANCH_COVER_PURPOSE = 'branch-cover';
const ORGANIZATION_LOGO_PURPOSE = 'organization-logo';

// Та же палитра, что в мастере установки: цвет выбирают один раз при установке, а меняют — здесь,
// и два разных набора образцов означали бы, что выбранное при установке тут не найти.
const BRAND_COLORS = ['#C8FF00', '#FF3B30', '#FF9F0A', '#30D158', '#0A84FF', '#BF5AF2', '#FF375F', '#64D2FF'];

const BRAND_COLOR_LABEL_KEYS: Record<string, MessageKey> = {
  '#C8FF00': 'setup.wizard.branding.color.lime',
  '#FF3B30': 'setup.wizard.branding.color.red',
  '#FF9F0A': 'setup.wizard.branding.color.orange',
  '#30D158': 'setup.wizard.branding.color.green',
  '#0A84FF': 'setup.wizard.branding.color.blue',
  '#BF5AF2': 'setup.wizard.branding.color.purple',
  '#FF375F': 'setup.wizard.branding.color.pink',
  '#64D2FF': 'setup.wizard.branding.color.sky'
};

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
  photos: GalleryPhoto[];
  // Координаты живут в форме строкой, а не числом: при вводе «38.» число проглотило бы точку
  // и продолжить набор дробной части стало бы невозможно. В число их превращает сборка запроса.
  latitude: string;
  longitude: string;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDay[];
}

/**
 * Оформление сети — отдельная запись, а не поле филиала: логотип и цвет принадлежат организации,
 * их видят гости в приложении и на экранах игровых ПК. Задавал их только мастер установки —
 * промахнулся с цветом при установке, и поменять было негде.
 */
export interface ClubBrandForm {
  logoUrl: string | null;
  accentColor: string | null;
}

const TIME_ZONES = ['Asia/Dushanbe', 'Asia/Tashkent', 'Asia/Almaty', 'Asia/Bishkek', 'Europe/Moscow', 'Asia/Yekaterinburg'];
const LOCALES: Array<{ value: string; key: MessageKey }> = [
  { value: 'ru', key: 'op.club.locale.ru' },
  { value: 'tg', key: 'op.club.locale.tg' },
  { value: 'en', key: 'op.club.locale.en' }
];

interface ClubProfileFieldsProps {
  form: ClubProfileForm;
  brand: ClubBrandForm;
  currencyCode: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
  onField: <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => void;
  onBrandField: <K extends keyof ClubBrandForm>(key: K, value: ClubBrandForm[K]) => void;
  // «Как видит игрок» кладём между панелями — грид (club-profile-layout) сам ставит его в правый
  // верхний угол, поэтому порядок в DOM не важен.
  preview: ReactNode;
}

// Две панели одинаковой сетки полей (единая ширина колонок = консистентность):
//  1. «Профиль» — лицо игрока + контакты (рядом превью).
//  2. «Часы и настройки» — 7-дневный график + пояс/язык/валюта (во всю ширину под превью).
export function ClubProfileFields({ form, brand, currencyCode, backend, disabled, onField, onBrandField, preview }: ClubProfileFieldsProps) {
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
            <label className="club-identity-gallery">{t('op.club.field.gallery')}
              <GalleryUpload
                value={form.photos}
                branchId={backend.branchId}
                backend={backend}
                disabled={disabled}
                onChange={(photos) => onField('photos', photos)}
              />
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

          {/* Бренд сети, а не зала: логотип и цвет уезжают в приложение гостя и на экраны игровых
              ПК. До сих пор их ставил только мастер установки — промахнулся с цветом при
              установке, и поменять было негде. */}
          <div className="mgmt-section-title"><span>{t('op.club.section.brand')}</span></div>
          <div className="club-field-grid">
            <label className="club-logo-field">{t('op.club.field.brandLogo')}
              <MediaUpload
                value={brand.logoUrl}
                purpose={ORGANIZATION_LOGO_PURPOSE}
                branchId={backend.branchId}
                backend={backend}
                disabled={disabled}
                onChange={(media) => onBrandField('logoUrl', media?.url ?? null)}
              />
            </label>
            <div className="club-brand-colors">
              <span className="club-brand-colors-label">{t('op.club.field.brandColor')}</span>
              <div role="radiogroup" aria-label={t('op.club.field.brandColor')} className="club-brand-swatches">
                {BRAND_COLORS.map((color) => (
                  <button
                    key={color}
                    type="button"
                    role="radio"
                    aria-checked={brand.accentColor === color}
                    aria-label={t(BRAND_COLOR_LABEL_KEYS[color] ?? 'op.club.field.brandColor')}
                    className={brand.accentColor === color ? 'club-brand-swatch is-selected' : 'club-brand-swatch'}
                    style={{ background: color }}
                    disabled={disabled}
                    onClick={() => onBrandField('accentColor', color)}
                  />
                ))}
              </div>
              <span className="club-field-hint">{t('op.club.hint.brand')}</span>
            </div>
          </div>
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

const skeletonFields = (count: number) => Array.from({ length: count }, (_, index) => (
  <label key={index}><SkeletonLine width={index % 2 === 0 ? '7em' : '9em'} /><SkeletonControl /></label>
));

// Заглушка профиля клуба: те же три области (профиль, превью, часы и настройки), те же заголовки
// секций и сетки полей. Подсказки — настоящим текстом: от них зависит высота, а они известны до
// ответа. Поля картинок — в пустом состоянии: есть ли уже логотип и сколько фото в галерее, до
// ответа неизвестно, и у клуба с фото карточка вырастет ниже.
// Поле загрузки картинки в пустом состоянии: крупная кнопка и подсказка о форматах (у логотипа в
// шапке подсказку прячет CSS, а кнопка там высотой обычного поля — отсюда `field`).
function MediaUploadSkeleton({ field = false }: { field?: boolean }) {
  const { t } = useI18n();
  return (
    <div className="media-upload">
      <div className="media-upload-actions"><SkeletonControl width="9rem" size={field ? undefined : 'lg'} /></div>
      <p className="media-upload-hint">{t('op.media.upload.hint')}</p>
    </div>
  );
}

export function ClubProfileSkeleton() {
  const { t } = useI18n();
  return (
    <div className="club-profile-layout" data-skeleton="form" aria-hidden="true">
      <section className="management-panel club-area-profile">
        <div className="mgmt-form">
          <div className="mgmt-section-title"><SkeletonLine width="10em" /></div>
          <div className="club-identity-grid">
            <label className="club-identity-name"><SkeletonLine width="7em" /><SkeletonControl /></label>
            <label className="club-logo-field club-identity-logo"><SkeletonLine width="5em" /><MediaUploadSkeleton field /></label>
            <label className="club-logo-field club-identity-cover">
              <SkeletonLine width="6em" />
              <MediaUploadSkeleton />
              <span className="club-field-hint">{t('op.club.hint.cover')}</span>
            </label>
            <label className="club-identity-gallery">
              <SkeletonLine width="6em" />
              <div className="gallery-upload"><div className="media-upload-actions"><SkeletonControl width="9rem" size="lg" /></div></div>
            </label>
            {/* Высота — как у textarea описания (.club-identity-desc textarea). */}
            <label className="club-identity-desc"><SkeletonLine width="8em" /><span className="skeleton-block" style={{ height: 130 }} /></label>
          </div>
          <div className="mgmt-section-title"><SkeletonLine width="8em" /></div>
          <div className="club-field-grid">{skeletonFields(8)}</div>
          <p className="club-field-hint">{t('op.club.hint.coords')}</p>
          <div className="mgmt-section-title"><SkeletonLine width="8em" /></div>
          <div className="club-field-grid">
            <label className="club-logo-field"><SkeletonLine width="7em" /><MediaUploadSkeleton /></label>
            <div className="club-brand-colors">
              <span className="club-brand-colors-label"><SkeletonLine width="8em" /></span>
              <div className="club-brand-swatches">
                {BRAND_COLORS.map((color) => <span key={color} className="club-brand-swatch skeleton-block" />)}
              </div>
              <span className="club-field-hint">{t('op.club.hint.brand')}</span>
            </div>
          </div>
        </div>
      </section>

      <aside className="club-preview club-area-preview">
        <div className="mgmt-section-title"><SkeletonLine width="8em" /></div>
        <div className="club-preview-card">
          <span className="club-preview-logo skeleton-block" />
          <div className="club-preview-name"><SkeletonLine width="60%" /></div>
          <div className="club-preview-address"><SkeletonLine width="80%" /></div>
          <div className="club-preview-today"><SkeletonLine width="50%" /></div>
        </div>
      </aside>

      <section className="management-panel club-area-schedule">
        <div className="mgmt-form">
          <div className="mgmt-section-title"><SkeletonLine width="8em" /></div>
          <div className="club-hours">
            <div className="club-hours-grid">
              {Array.from({ length: 7 }, (_, day) => <div key={day} className="club-hours-row"><SkeletonControl /></div>)}
            </div>
          </div>
          <div className="mgmt-section-title"><SkeletonLine width="8em" /></div>
          <div className="club-field-grid">{skeletonFields(3)}</div>
        </div>
      </section>
    </div>
  );
}
