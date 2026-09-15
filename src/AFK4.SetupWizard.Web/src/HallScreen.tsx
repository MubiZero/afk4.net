import { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { WizardZone } from './wizardApi';

export interface HallClient {
  createSeats(zoneId: string, namePrefix: string, count: number): Promise<{ names: string[] }>;
}

interface HallScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: HallClient;
  zones: WizardZone[];
  ownerName: string;
  branchName: string;
  onContinue(): void;
  onBack(): void;
}

export function HallScreen({ stepNumber, client, zones, ownerName, branchName, onContinue, onBack }: HallScreenProps) {
  const { t } = useI18n();
  const [zoneId, setZoneId] = useState(zones[0]?.zoneId ?? '');
  // «ПК» — канон терминов проекта, поэтому и место называется так же. Но строка всё равно из
  // каталога: мастер переключается на en/tg прямо в титлбаре, и подставлять кириллицу в
  // английский интерфейс нельзя.
  const defaultPrefix = t('setup.wizard.hall.prefixDefault');
  const [namePrefix, setNamePrefix] = useState(defaultPrefix);
  const [count, setCount] = useState('10');
  const [createdNames, setCreatedNames] = useState<string[]>([]);
  const [creating, setCreating] = useState(false);
  const [failed, setFailed] = useState(false);

  const parsedCount = Number.parseInt(count, 10);
  const canCreate =
    zoneId !== '' && namePrefix.trim() !== '' && Number.isFinite(parsedCount) && parsedCount > 0 && !creating;

  async function create(): Promise<void> {
    if (!canCreate) return;
    setCreating(true);
    setFailed(false);
    try {
      const result = await client.createSeats(zoneId, namePrefix.trim(), parsedCount);
      setCreatedNames(result.names);
    } catch {
      setFailed(true);
    } finally {
      setCreating(false);
    }
  }

  return (
    <section className="wizard-screen is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>{stepNumber}</span>
          <h1>{t('setup.wizard.hall.title')}</h1>
        </div>
        <p>{t('setup.wizard.hall.subtitle')}</p>
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="hall-zone">{t('setup.wizard.hall.zone')}</label>
        <select
          id="hall-zone"
          value={zoneId}
          onChange={(event) => setZoneId(event.target.value)}
        >
          {zones.map((zone) => (
            <option key={zone.zoneId} value={zone.zoneId}>{zone.name}</option>
          ))}
        </select>
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="hall-prefix">{t('setup.wizard.hall.prefix')}</label>
        <input
          id="hall-prefix"
          value={namePrefix}
          onChange={(event) => setNamePrefix(event.target.value)}
        />
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="hall-count">{t('setup.wizard.hall.count')}</label>
        <input
          id="hall-count"
          type="number"
          min={1}
          max={60}
          value={count}
          onChange={(event) => setCount(event.target.value)}
        />
        <small>
          {t('setup.wizard.hall.example', {
            name: `${namePrefix.trim() || defaultPrefix}-1`,
            name2: `${namePrefix.trim() || defaultPrefix}-2`,
          })}
        </small>
      </div>

      <button type="button" className="wizard-secondary" onClick={() => void create()} disabled={!canCreate}>
        {creating ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : <Check size={16} aria-hidden />}
        {t('setup.wizard.hall.create')}
      </button>

      {failed ? <p className="ui-alert">{t('setup.wizard.hall.failed')}</p> : null}
      {createdNames.length > 0 ? (
        <p className="wizard-field-hint">{t('setup.wizard.hall.created', { count: createdNames.length })}</p>
      ) : null}

      <div className="wizard-actions">
        <button type="button" className="wizard-secondary" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button type="button" className="wizard-primary" onClick={onContinue} disabled={creating}>
          <ArrowRight size={16} aria-hidden />
          {createdNames.length > 0 ? t('setup.wizard.hall.next') : t('setup.wizard.hall.skip')}
        </button>
      </div>
    </section>
  );
}
