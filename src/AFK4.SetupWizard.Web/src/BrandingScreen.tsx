import { useEffect, useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { wizardErrorMessage } from './wizardErrors';
import type { WizardBrandingPreset } from './wizardApi';
import { handleRadioGroupKeys, radioTabIndex } from './radioGroup';

// Цвет клуба доезжает до приложения игрока и до оболочки игрового ПК. Палитра задана здесь, а не
// свободным вводом: на первом шаге важнее быстро выбрать читаемый цвет, чем подобрать оттенок.
const COLORS = ['#C8FF00', '#FF3B30', '#FF9F0A', '#30D158', '#0A84FF', '#BF5AF2', '#FF375F', '#64D2FF'];

// Названия цветов для тех, кто не видит образцы. Диктор читал сам код — «решётка це восемь эф эф
// ноль ноль», — и выбрать по нему было невозможно.
const COLOR_LABEL_KEYS: Record<string, MessageKey> = {
  '#C8FF00': 'setup.wizard.branding.color.lime',
  '#FF3B30': 'setup.wizard.branding.color.red',
  '#FF9F0A': 'setup.wizard.branding.color.orange',
  '#30D158': 'setup.wizard.branding.color.green',
  '#0A84FF': 'setup.wizard.branding.color.blue',
  '#BF5AF2': 'setup.wizard.branding.color.purple',
  '#FF375F': 'setup.wizard.branding.color.pink',
  '#64D2FF': 'setup.wizard.branding.color.sky'
};

// Клиент приходит пропсом, как у DeviceScreen: экран не знает про мост, а тест не подменяет модуль.
export interface BrandingClient {
  presets(): Promise<{ presets: WizardBrandingPreset[] }>;
  save(logoUrl: string | null, accentColor: string | null): Promise<{ saved: boolean }>;
  uploadLogo(): Promise<{ logoUrl?: string | null }>;
}

interface BrandingScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: BrandingClient;
  ownerName: string;
  branchName: string;
  onContinue(): void;
  onBack(): void;
}

export function BrandingScreen({ stepNumber, client, ownerName, branchName, onContinue, onBack }: BrandingScreenProps) {
  const { t } = useI18n();
  // null — ещё грузим. Пустой массив — пресетов не будет (отказ или их правда нет): шаг всё
  // равно рабочий. Раньше оба состояния выглядели одинаково пустым блоком, и на телефонном
  // интернете раздел читался как сломанный.
  const [presets, setPresets] = useState<WizardBrandingPreset[] | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [accentColor, setAccentColor] = useState<string>(COLORS[0]);
  const [saving, setSaving] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadFailure, setUploadFailure] = useState<string | null>(null);
  const [ownLogoUrl, setOwnLogoUrl] = useState<string | null>(null);

  // Спиннер с задержкой: на быстрой сети он бы мелькнул и только дёрнул глаз. Тот же приём и с
  // тем же порогом, что на экране входа.
  const [showPresetsSkeleton, setShowPresetsSkeleton] = useState(false);
  useEffect(() => {
    if (presets !== null) {
      setShowPresetsSkeleton(false);
      return;
    }
    const timer = setTimeout(() => setShowPresetsSkeleton(true), 300);
    return () => clearTimeout(timer);
  }, [presets]);

  useEffect(() => {
    let cancelled = false;
    client
      .presets()
      .then((result) => {
        if (!cancelled) setPresets(result.presets);
      })
      .catch(() => {
        // Без пресетов шаг всё равно рабочий: цвет выбирается, логотип ставится позже в панели.
        if (!cancelled) setPresets([]);
      });
    return () => {
      cancelled = true;
    };
  }, [client]);

  async function upload(): Promise<void> {
    setUploading(true);
    setUploadFailure(null);
    try {
      const result = await client.uploadLogo();
      // Пустой ответ — человек закрыл окно выбора: это не ошибка, экран остаётся как был.
      // Поле может не прийти вовсе: пустые значения мост не сериализует.
      if (result.logoUrl) {
        setOwnLogoUrl(result.logoUrl);
        setLogoUrl(result.logoUrl);
      }
    } catch (error) {
      setUploadFailure(wizardErrorMessage(error, t, 'setup.wizard.branding.uploadFailed'));
    } finally {
      setUploading(false);
    }
  }

  async function save(): Promise<void> {
    setSaving(true);
    setFailure(null);
    try {
      await client.save(logoUrl, accentColor);
      onContinue();
    } catch (error) {
      setFailure(wizardErrorMessage(error, t, 'setup.wizard.branding.failed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="wizard-screen is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>{stepNumber}</span>
          <h1>{t('setup.wizard.branding.title')}</h1>
        </div>
        <p>{t('setup.wizard.branding.subtitle')}</p>
      </div>

      <div className="ui-field">
        <span className="ui-field-label">{t('setup.wizard.branding.logo')}</span>
        {/* Не radiogroup: нажатие по выбранному снимает выбор, а переключатель так не умеет —
            выбранный остаётся выбранным, пока не выбрали другой. Здесь это кнопки-переключатели,
            и «без логотипа» — законное состояние: клуб откроется и без него. */}
        {presets === null && showPresetsSkeleton && (
          <p className="ui-field-hint" role="status">{t('setup.wizard.branding.logoLoading')}</p>
        )}
        {presets !== null && presets.length === 0 && (
          <p className="ui-field-hint">{t('setup.wizard.branding.logoUnavailable')}</p>
        )}

        <div className="wizard-preset-grid" role="group" aria-label={t('setup.wizard.branding.logo')}>
          {(presets ?? []).map((preset, index) => (
            <button
              key={preset.id}
              type="button"
              aria-pressed={logoUrl === preset.url}
              // Не `preset.id`: диктор читал служебное имя файла вместо «вариант 2».
              aria-label={t('setup.wizard.branding.logoOption', { index: index + 1 })}
              className={logoUrl === preset.url ? 'wizard-preset is-selected' : 'wizard-preset'}
              style={{ background: accentColor }}
              onClick={() => setLogoUrl(logoUrl === preset.url ? null : preset.url)}
            >
              <img src={preset.url} alt="" />
              {logoUrl === preset.url ? <Check size={16} aria-hidden /> : null}
            </button>
          ))}
        </div>
      </div>

      <div className="ui-field">
        <button type="button" className="ui-btn" onClick={() => void upload()} disabled={uploading}>
          {uploading ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : null}
          {t('setup.wizard.branding.upload')}
        </button>
        {ownLogoUrl === null ? null : (
          <span className="wizard-preset is-selected" style={{ background: accentColor }}>
            <img src={ownLogoUrl} alt={t('setup.wizard.branding.ownLogo')} />
          </span>
        )}
        {uploadFailure === null ? null : <p className="ui-alert" role="alert">{uploadFailure}</p>}
      </div>

      <div className="ui-field">
        <span className="ui-field-label">{t('setup.wizard.branding.color')}</span>
        <div
          className="wizard-color-row"
          role="radiogroup"
          aria-label={t('setup.wizard.branding.color')}
          onKeyDown={(event) => handleRadioGroupKeys(event, COLORS, accentColor, setAccentColor)}
        >
          {COLORS.map((color, index) => (
            <button
              key={color}
              type="button"
              role="radio"
              aria-checked={accentColor === color}
              tabIndex={radioTabIndex(index, COLORS.indexOf(accentColor))}
              // Не сам код цвета: диктор читал «решётка це восемь эф эф ноль ноль».
              aria-label={t(COLOR_LABEL_KEYS[color] ?? 'setup.wizard.branding.color')}
              className={accentColor === color ? 'wizard-color is-selected' : 'wizard-color'}
              style={{ background: color }}
              onClick={() => setAccentColor(color)}
            />
          ))}
        </div>
      </div>

      {failure === null ? null : <p className="ui-alert" role="alert">{failure}</p>}

      <div className="wizard-actions">
        <button type="button" className="ui-btn" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        {/* Оформление можно пропустить: клуб откроется и без логотипа, а поставить его
            управляющий сможет в панели. */}
        <button type="button" className="ui-btn" onClick={onContinue} disabled={saving}>
          {t('setup.wizard.branding.skip')}
        </button>
        <button type="button" className="ui-btn ui-btn--primary" onClick={() => void save()} disabled={saving}>
          {saving ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : <ArrowRight size={16} aria-hidden />}
          {t('setup.wizard.branding.save')}
        </button>
      </div>
    </section>
  );
}
