import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { LoadFailureState } from '../../../operatorPrimitives';
import { DeferredSkeleton } from '../../../LoadingSkeleton';
import { SetupRuleSkeleton } from '../../kit/SetupSection';
import { projectOperatorError, type OperatorErrorProjection } from '../../../apiErrors';
import { createAuthenticatedOperatorClients } from '../../../operatorHelpers';
import type { OperatorBackendContext } from '../../../operatorTypes';
import type { TipSettingsDto } from '@afk4/contracts';

// Чаевые администратору с экрана ПК. Одна настройка — и сохраняется она сразу, по щелчку: кнопка
// «Сохранить» под единственным переключателем была бы лишним шагом. Переключатель встаёт на новое
// место только после ответа сервера — это деньги игроков, а не оформление.
interface TipSettingsClient {
  getSettings(): Promise<TipSettingsDto>;
  updateSettings(request: { enabled: boolean }): Promise<TipSettingsDto>;
}

export function TipsSection({ backend, client: injected }: { backend: OperatorBackendContext; client?: TipSettingsClient }) {
  const { t } = useI18n();
  const client = useMemo(
    () => injected ?? createAuthenticatedOperatorClients(backend.config, backend.session).tips,
    [injected, backend.config, backend.session]
  );
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<OperatorErrorProjection | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const load = () => {
    setLoadError(null);
    client.getSettings()
      .then((settings) => setEnabled(settings.enabled))
      .catch((error) => setLoadError(projectOperatorError(error, t)));
  };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(load, [client]);

  const toggle = async (next: boolean) => {
    setSaving(true);
    setSaveError(null);
    try {
      setEnabled((await client.updateSettings({ enabled: next })).enabled);
    } catch (error) {
      setSaveError(projectOperatorError(error, t).detail);
    } finally {
      setSaving(false);
    }
  };

  if (loadError) {
    return <LoadFailureState title={t('op.management.state.errorTitle')} failure={loadError} onRetry={load} />;
  }

  if (enabled === null) {
    return <DeferredSkeleton><SetupRuleSkeleton hint={t('op.tips.enabledHint')} /></DeferredSkeleton>;
  }

  return (
    <>
      <div className={`payset-rule${enabled ? ' is-on' : ''}`}>
        <div className="payset-rule-top">
          <label className="payset-switch">
            <input
              type="checkbox"
              aria-label={t('op.tips.enabled')}
              checked={enabled}
              disabled={saving}
              onChange={(event) => void toggle(event.currentTarget.checked)}
            />
            <span className="payset-track" />
            <span className="payset-knob" />
          </label>
          <div className="payset-rule-text">
            <div className="payset-rule-name">{t('op.tips.enabled')}</div>
            <div className="payset-rule-hint">{t('op.tips.enabledHint')}</div>
          </div>
        </div>
      </div>
      <p className="payset-field-hint">{t('op.tips.moneyHint')}</p>
      {saveError && <p className="ui-inline-error" role="alert">{saveError}</p>}
    </>
  );
}
