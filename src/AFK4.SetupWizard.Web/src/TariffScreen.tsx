import { useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { majorToMinor } from '@afk4/money';
import { wizardErrorMessage } from './wizardErrors';

export interface TariffClient {
  createTariff(name: string, pricePerHourMinorUnits: number): Promise<{ name: string }>;
}

/// Введённое на экране. Живёт в App: экран монтируется заново на каждом шаге, и при «Назад»
/// созданный тариф снова предлагался к созданию — а тот же тариф сервер второй раз не примет.
export interface TariffDraft {
  name: string;
  pricePerHour: string;
  created: string | null;
}

interface TariffScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: TariffClient;
  ownerName: string;
  branchName: string;
  /// Что было введено при прошлом заходе на шаг; null — заход первый.
  initialDraft?: TariffDraft | null;
  onContinue(draft: TariffDraft): void;
  onBack(draft: TariffDraft): void;
}

export function TariffScreen({
  stepNumber,
  client,
  ownerName,
  branchName,
  initialDraft = null,
  onContinue,
  onBack,
}: TariffScreenProps) {
  const { t } = useI18n();
  const [name, setName] = useState(initialDraft?.name ?? t('setup.wizard.tariff.defaultName'));
  const [pricePerHour, setPricePerHour] = useState(initialDraft?.pricePerHour ?? '10');
  const [created, setCreated] = useState<string | null>(initialDraft?.created ?? null);
  const [saving, setSaving] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const parsedPrice = Number.parseFloat(pricePerHour.replace(',', '.'));
  const canCreate = name.trim() !== '' && Number.isFinite(parsedPrice) && parsedPrice > 0 && !saving;
  const draft: TariffDraft = { name, pricePerHour, created };

  async function create(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (!canCreate) return;
    setSaving(true);
    setFailure(null);
    try {
      // Цена вводится в сомони, а хранится в дирамах: копейки считаются целыми, иначе округление
      // однажды съест или подарит минуту игры. Перевод — общий на весь проект: свой
      // `Math.round(x * 100)` округлял 1.005 вниз, потому что в двоичной дроби это 1.00499999…,
      // и мастер расходился с остальными экранами на дирам.
      const result = await client.createTariff(name.trim(), majorToMinor(parsedPrice));
      setCreated(result.name);
    } catch (error) {
      setFailure(wizardErrorMessage(error, t, 'setup.wizard.tariff.failed'));
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
          <h1>{t('setup.wizard.tariff.title')}</h1>
        </div>
        <p>{t('setup.wizard.tariff.subtitle')}</p>
      </div>

      {/* Форма — ради Enter: набрал цену и жмёт Enter, как на входе и на экране устройства.
          Поля вне формы Enter молча проглатывали. «Дальше» в форму не входит: Enter в поле
          создаёт тариф, а не уводит со шага. */}
      <form className="wizard-form" onSubmit={create} noValidate>
        <div className="ui-field">
          <label className="ui-field-label" htmlFor="tariff-name">{t('setup.wizard.tariff.name')}</label>
          <input
            id="tariff-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </div>

        <div className="ui-field">
          <label className="ui-field-label" htmlFor="tariff-price">{t('setup.wizard.tariff.price')}</label>
          <input
            id="tariff-price"
            type="number"
            min={1}
            step="0.5"
            value={pricePerHour}
            onChange={(event) => setPricePerHour(event.target.value)}
          />
        </div>

        <button type="submit" className="ui-btn" disabled={!canCreate}>
          {saving ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : <Check size={16} aria-hidden />}
          {t('setup.wizard.tariff.create')}
        </button>
      </form>

      {failure === null ? null : <p className="ui-alert" role="alert">{failure}</p>}
      {created === null ? null : <p className="ui-field-hint">{t('setup.wizard.tariff.created', { name: created })}</p>}

      <div className="wizard-actions">
        <button type="button" className="ui-btn" onClick={() => onBack(draft)}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button type="button" className="ui-btn ui-btn--primary" onClick={() => onContinue(draft)} disabled={saving}>
          <ArrowRight size={16} aria-hidden />
          {created === null ? t('setup.wizard.tariff.skip') : t('setup.wizard.tariff.next')}
        </button>
      </div>
    </section>
  );
}
