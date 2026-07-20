import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { projectOperatorError } from '../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import type { Feedback } from '../../operatorTypes';
import type { DestinationProps } from './types';

// Проценты кэшбэка хранятся на сервере в basis points (1% = 100 bp), в UI показываем целые
// проценты. Возврат null = поле вне диапазона 0..100 → не сохраняем. Портировано 1:1 из прежнего
// LoyaltySettingsWorkspace (money-контракт loyaltySettings не трогаем).
function toBasisPoints(percent: string): number | null {
  const value = Number(percent);
  if (!Number.isFinite(value) || value < 0 || value > 100) return null;
  return Math.round(value * 100);
}

// Лояльность: форма настроек кэшбэка по эталону «Клуб» (ClubDestination) — загрузка/сохранение
// живут здесь, save-бар и loading/error рисует ManagementScreen (одна кнопка на экран, скелетон
// при загрузке, панель ошибки с «Повторить»), результат сохранения — тостом (useFeedbackToasts).
// Реальный dirty-tracking включает unsaved-guard. Одна сущность на организацию (два правила:
// пополнение/магазин), поэтому это форма, а не list+drawer.
export function LoyaltyDestination({ backend, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const [topUpEnabled, setTopUpEnabled] = useState(false);
  const [topUpPercent, setTopUpPercent] = useState('0');
  const [shopEnabled, setShopEnabled] = useState(false);
  const [shopPercent, setShopPercent] = useState('0');
  const [sessionEnabled, setSessionEnabled] = useState(false);
  const [sessionPercent, setSessionPercent] = useState('0');
  const [cashbackCap, setCashbackCap] = useState('0');
  const [minimumSource, setMinimumSource] = useState('0');
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [ready, setReady] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  const load = useCallback(() => {
    if (backend === null) return undefined;
    let active = true;
    setLoadError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.loyaltySettings.get()
      .then((settings) => {
        if (!active) return;
        setTopUpEnabled(settings.topUpEnabled);
        setTopUpPercent(String(settings.topUpPercentBasisPoints / 100));
        setShopEnabled(settings.shopEnabled);
        setShopPercent(String(settings.shopPercentBasisPoints / 100));
        setSessionEnabled(settings.sessionEnabled);
        setSessionPercent(String(settings.sessionPercentBasisPoints / 100));
        setCashbackCap(formatMoneyInputMinorUnits(settings.cashbackCapMinorUnits));
        setMinimumSource(formatMoneyInputMinorUnits(settings.minimumSourceMinorUnits));
        setDirty(false);
        setReady(true);
      })
      .catch((error) => {
        if (active) setLoadError(projectOperatorError(error, t).detail);
      });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config, backend?.session]);

  useEffect(() => load(), [load]);

  useEffect(() => {
    onDirtyChange?.(dirty);
  }, [dirty, onDirtyChange]);

  const markDirty = () => {
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    const topUpBps = toBasisPoints(topUpPercent);
    const shopBps = toBasisPoints(shopPercent);
    const sessionBps = toBasisPoints(sessionPercent);
    if (topUpBps === null || shopBps === null || sessionBps === null) {
      setFeedback({ label: t('op.loyalty.feedbackLabel'), state: 'failed', detail: t('op.loyalty.percentError') });
      return;
    }
    const capMinor = parseNonNegativeMoneyInputMinorUnits(cashbackCap);
    const minimumMinor = parseNonNegativeMoneyInputMinorUnits(minimumSource);
    if (capMinor === null || minimumMinor === null) {
      setFeedback({ label: t('op.loyalty.feedbackLabel'), state: 'failed', detail: t('op.loyalty.limitError') });
      return;
    }

    setSaving(true);
    setFeedback({ label: t('op.loyalty.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const settings = await clients.loyaltySettings.update({
        topUpEnabled,
        topUpPercentBasisPoints: topUpBps,
        shopEnabled,
        shopPercentBasisPoints: shopBps,
        sessionEnabled,
        sessionPercentBasisPoints: sessionBps,
        cashbackCapMinorUnits: capMinor,
        minimumSourceMinorUnits: minimumMinor
      });
      setTopUpEnabled(settings.topUpEnabled);
      setTopUpPercent(String(settings.topUpPercentBasisPoints / 100));
      setShopEnabled(settings.shopEnabled);
      setShopPercent(String(settings.shopPercentBasisPoints / 100));
      setSessionEnabled(settings.sessionEnabled);
      setSessionPercent(String(settings.sessionPercentBasisPoints / 100));
      setCashbackCap(formatMoneyInputMinorUnits(settings.cashbackCapMinorUnits));
      setMinimumSource(formatMoneyInputMinorUnits(settings.minimumSourceMinorUnits));
      setDirty(false);
      setSaved(true);
      setFeedback({ label: t('op.loyalty.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.loyalty.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';
  const state = loadError ? 'error' : (backend !== null && !ready) ? 'loading' : 'ready';

  return (
    <ManagementScreen
      title={t('op.management.dest.loyalty')}
      subtitle={t('op.management.dest.loyalty.subtitle')}
      state={state}
      errorDetail={loadError ?? undefined}
      onRetry={() => load()}
      save={{ state: saveState, onSave: () => void save(), disabled: backend === null }}
    >
      <div className="management-panel">
        <div className="mgmt-form">
          <div className="loyalty-rules">
            <div className="loyalty-rule">
              <label className="mgmt-check loyalty-rule-toggle">
                <input
                  type="checkbox"
                  checked={topUpEnabled}
                  disabled={backend === null || saving}
                  onChange={(event) => { setTopUpEnabled(event.currentTarget.checked); markDirty(); }}
                />
                <span className="loyalty-rule-text">
                  <span className="loyalty-rule-name">{t('op.loyalty.topUpEnabled')}</span>
                  <span className="loyalty-rule-hint">{t('op.loyalty.topUpHint')}</span>
                </span>
              </label>
              <label className="loyalty-rule-percent">
                {t('op.loyalty.topUpPercent')}
                <input
                  type="number"
                  min="0"
                  max="100"
                  value={topUpPercent}
                  disabled={backend === null || saving || !topUpEnabled}
                  onChange={(event) => { setTopUpPercent(event.currentTarget.value); markDirty(); }}
                />
              </label>
            </div>

            <div className="loyalty-rule">
              <label className="mgmt-check loyalty-rule-toggle">
                <input
                  type="checkbox"
                  checked={shopEnabled}
                  disabled={backend === null || saving}
                  onChange={(event) => { setShopEnabled(event.currentTarget.checked); markDirty(); }}
                />
                <span className="loyalty-rule-text">
                  <span className="loyalty-rule-name">{t('op.loyalty.shopEnabled')}</span>
                  <span className="loyalty-rule-hint">{t('op.loyalty.shopHint')}</span>
                </span>
              </label>
              <label className="loyalty-rule-percent">
                {t('op.loyalty.shopPercent')}
                <input
                  type="number"
                  min="0"
                  max="100"
                  value={shopPercent}
                  disabled={backend === null || saving || !shopEnabled}
                  onChange={(event) => { setShopPercent(event.currentTarget.value); markDirty(); }}
                />
              </label>
            </div>

            <div className="loyalty-rule">
              <label className="mgmt-check loyalty-rule-toggle">
                <input
                  type="checkbox"
                  checked={sessionEnabled}
                  disabled={backend === null || saving}
                  onChange={(event) => { setSessionEnabled(event.currentTarget.checked); markDirty(); }}
                />
                <span className="loyalty-rule-text">
                  <span className="loyalty-rule-name">{t('op.loyalty.sessionEnabled')}</span>
                  <span className="loyalty-rule-hint">{t('op.loyalty.sessionHint')}</span>
                </span>
              </label>
              <label className="loyalty-rule-percent">
                {t('op.loyalty.sessionPercent')}
                <input
                  type="number"
                  min="0"
                  max="100"
                  value={sessionPercent}
                  disabled={backend === null || saving || !sessionEnabled}
                  onChange={(event) => { setSessionPercent(event.currentTarget.value); markDirty(); }}
                />
              </label>
            </div>
          </div>

          <div className="loyalty-limits">
            <div className="mgmt-section-title"><span>{t('op.loyalty.limits.title')}</span></div>
            <div className="mgmt-form-grid">
              <label>{`${t('op.loyalty.cap')}, ${currencyCode}`}
                <input
                  inputMode="decimal"
                  value={cashbackCap}
                  disabled={backend === null || saving}
                  onChange={(event) => { setCashbackCap(event.currentTarget.value); markDirty(); }}
                />
                <span className="settings-field-hint">{t('op.loyalty.capHint')}</span>
              </label>
              <label>{`${t('op.loyalty.minimum')}, ${currencyCode}`}
                <input
                  inputMode="decimal"
                  value={minimumSource}
                  disabled={backend === null || saving}
                  onChange={(event) => { setMinimumSource(event.currentTarget.value); markDirty(); }}
                />
                <span className="settings-field-hint">{t('op.loyalty.minimumHint')}</span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </ManagementScreen>
  );
}
