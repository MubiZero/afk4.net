import { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { majorToMinor } from '@afk4/money';

export interface TariffClient {
  createTariff(name: string, pricePerHourMinorUnits: number): Promise<{ name: string }>;
}

interface TariffScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: TariffClient;
  ownerName: string;
  branchName: string;
  onContinue(): void;
  onBack(): void;
}

export function TariffScreen({ stepNumber, client, ownerName, branchName, onContinue, onBack }: TariffScreenProps) {
  const { t } = useI18n();
  const [name, setName] = useState(t('setup.wizard.tariff.defaultName'));
  const [pricePerHour, setPricePerHour] = useState('10');
  const [created, setCreated] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [failed, setFailed] = useState(false);

  const parsedPrice = Number.parseFloat(pricePerHour.replace(',', '.'));
  const canCreate = name.trim() !== '' && Number.isFinite(parsedPrice) && parsedPrice > 0 && !saving;

  async function create(): Promise<void> {
    if (!canCreate) return;
    setSaving(true);
    setFailed(false);
    try {
      // Цена вводится в сомони, а хранится в дирамах: копейки считаются целыми, иначе округление
      // однажды съест или подарит минуту игры. Перевод — общий на весь проект: свой
      // `Math.round(x * 100)` округлял 1.005 вниз, потому что в двоичной дроби это 1.00499999…,
      // и мастер расходился с остальными экранами на дирам.
      const result = await client.createTariff(name.trim(), majorToMinor(parsedPrice));
      setCreated(result.name);
    } catch {
      setFailed(true);
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

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="tariff-name">{t('setup.wizard.tariff.name')}</label>
        <input
          id="tariff-name"
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="tariff-price">{t('setup.wizard.tariff.price')}</label>
        <input
          id="tariff-price"
          type="number"
          min={1}
          step="0.5"
          value={pricePerHour}
          onChange={(event) => setPricePerHour(event.target.value)}
        />
      </div>

      <button type="button" className="wizard-secondary" onClick={() => void create()} disabled={!canCreate}>
        {saving ? <Loader2 size={16} className="wizard-spinner" aria-hidden /> : <Check size={16} aria-hidden />}
        {t('setup.wizard.tariff.create')}
      </button>

      {failed ? <p className="wizard-alert">{t('setup.wizard.tariff.failed')}</p> : null}
      {created === null ? null : <p className="wizard-field-hint">{t('setup.wizard.tariff.created', { name: created })}</p>}

      <div className="wizard-actions">
        <button type="button" className="wizard-secondary" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button type="button" className="wizard-primary" onClick={onContinue} disabled={saving}>
          <ArrowRight size={16} aria-hidden />
          {created === null ? t('setup.wizard.tariff.skip') : t('setup.wizard.tariff.next')}
        </button>
      </div>
    </section>
  );
}
