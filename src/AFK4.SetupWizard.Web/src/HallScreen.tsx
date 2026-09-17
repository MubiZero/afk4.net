import { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { WizardZone } from './wizardApi';
import { wizardErrorMessage } from './wizardErrors';

export interface HallClient {
  createSeats(zoneId: string, namePrefix: string, count: number): Promise<{ names: string[] }>;
}

/// Столько мест мастер заводит за один раз. Ограничение живёт на стороне хоста
/// (SetupWizardWebHostBridge.MaxSeatsPerRun) и проверяется там же; здесь оно нужно, чтобы не
/// отправлять заведомо отвергнутый запрос и показать предел словами, а не общим «не удалось».
/// Совпадение двух чисел стережёт hallSeatLimit.test.ts.
export const MAX_SEATS_PER_RUN = 60;

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
  const [failure, setFailure] = useState<string | null>(null);

  const parsedCount = Number.parseInt(count, 10);
  const countTooBig = Number.isFinite(parsedCount) && parsedCount > MAX_SEATS_PER_RUN;
  const canCreate =
    zoneId !== ''
    && namePrefix.trim() !== ''
    && Number.isFinite(parsedCount)
    && parsedCount > 0
    && !countTooBig
    && !creating;

  async function create(): Promise<void> {
    if (!canCreate) return;
    setCreating(true);
    setFailure(null);
    try {
      const result = await client.createSeats(zoneId, namePrefix.trim(), parsedCount);
      setCreatedNames(result.names);
    } catch (error) {
      setFailure(wizardErrorMessage(error, t, 'setup.wizard.hall.failed'));
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

      <div className="ui-field">
        <label className="ui-field-label" htmlFor="hall-zone">{t('setup.wizard.hall.zone')}</label>
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

      <div className="ui-field">
        <label className="ui-field-label" htmlFor="hall-prefix">{t('setup.wizard.hall.prefix')}</label>
        <input
          id="hall-prefix"
          value={namePrefix}
          onChange={(event) => setNamePrefix(event.target.value)}
        />
      </div>

      <div className="ui-field">
        <label className="ui-field-label" htmlFor="hall-count">{t('setup.wizard.hall.count')}</label>
        <input
          id="hall-count"
          type="number"
          min={1}
          max={MAX_SEATS_PER_RUN}
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

      <button type="button" className="ui-btn" onClick={() => void create()} disabled={!canCreate}>
        {creating ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : <Check size={16} aria-hidden />}
        {t('setup.wizard.hall.create')}
      </button>

      {countTooBig && (
        <p className="ui-field-hint">{t('setup.wizard.hall.tooMany', { max: MAX_SEATS_PER_RUN })}</p>
      )}

      {failure === null ? null : (
        <>
          <p className="ui-alert" role="alert">{failure}</p>
          {/* Места заводятся по одному, и обрыв связи на середине оставляет часть заведёнными.
              Повтор безопасен: место с тем же именем сервер не задваивает, а возвращает
              существующее. Без этой строки человек боится нажать и идёт в панель считать руками. */}
          <p className="ui-field-hint">{t('setup.wizard.hall.retrySafe')}</p>
        </>
      )}
      {createdNames.length > 0 ? (
        <p className="ui-field-hint">{t('setup.wizard.hall.created', { count: createdNames.length })}</p>
      ) : (
        /* Шаг можно пройти мимо, и это законно — но чем это обернётся, человек должен узнать
           здесь, а не открыв пустую карту зала перед первым гостем. */
        <p className="ui-field-hint">{t('setup.wizard.hall.skipMeaning')}</p>
      )}

      <div className="wizard-actions">
        <button type="button" className="ui-btn" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button type="button" className="ui-btn ui-btn--primary" onClick={onContinue} disabled={creating}>
          <ArrowRight size={16} aria-hidden />
          {createdNames.length > 0 ? t('setup.wizard.hall.next') : t('setup.wizard.hall.skip')}
        </button>
      </div>
    </section>
  );
}
