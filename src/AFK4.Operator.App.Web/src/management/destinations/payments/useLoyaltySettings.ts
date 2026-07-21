import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SaveState } from '../../ManagementScreen';
import { projectOperatorError } from '../../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../../../operatorHelpers';
import { useFeedbackToasts } from '../../../useFeedbackToasts';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

// Проценты кэшбэка хранятся на сервере в basis points (1% = 100 bp), в UI — целые проценты.
// null = поле вне диапазона 0..100 → не сохраняем.
function toBasisPoints(percent: string): number | null {
  const value = Number(percent);
  if (!Number.isFinite(value) || value < 0 || value > 100) return null;
  return Math.round(value * 100);
}

export interface LoyaltySettingsController {
  topUpEnabled: boolean; setTopUpEnabled: (value: boolean) => void;
  topUpPercent: string; setTopUpPercent: (value: string) => void;
  shopEnabled: boolean; setShopEnabled: (value: boolean) => void;
  shopPercent: string; setShopPercent: (value: string) => void;
  sessionEnabled: boolean; setSessionEnabled: (value: boolean) => void;
  sessionPercent: string; setSessionPercent: (value: string) => void;
  cashbackCap: string; setCashbackCap: (value: string) => void;
  minimumSource: string; setMinimumSource: (value: string) => void;
  saveState: SaveState;
  dirty: boolean;
  ready: boolean;
  loadError: string | null;
  disabled: boolean;
  save: () => Promise<void>;
  retry: () => void;
}

// Загрузка/сохранение настроек лояльности для зоны «Как вы возвращаете». Контроллер отдаёт
// save/state/dirty секционной кнопке внутри LoyaltySection (глобального save-бара на экране нет).
// Money-контракт loyaltySettings не трогаем.
export function useLoyaltySettings(backend: OperatorBackendContext | null, enabled = true): LoyaltySettingsController {
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

  const applySettings = useCallback((settings: {
    topUpEnabled: boolean; topUpPercentBasisPoints: number;
    shopEnabled: boolean; shopPercentBasisPoints: number;
    sessionEnabled: boolean; sessionPercentBasisPoints: number;
    cashbackCapMinorUnits: number; minimumSourceMinorUnits: number;
  }) => {
    setTopUpEnabled(settings.topUpEnabled);
    setTopUpPercent(String(settings.topUpPercentBasisPoints / 100));
    setShopEnabled(settings.shopEnabled);
    setShopPercent(String(settings.shopPercentBasisPoints / 100));
    setSessionEnabled(settings.sessionEnabled);
    setSessionPercent(String(settings.sessionPercentBasisPoints / 100));
    setCashbackCap(formatMoneyInputMinorUnits(settings.cashbackCapMinorUnits));
    setMinimumSource(formatMoneyInputMinorUnits(settings.minimumSourceMinorUnits));
  }, []);

  const load = useCallback(() => {
    if (backend === null || !enabled) return undefined;
    let active = true;
    setLoadError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.loyaltySettings.get()
      .then((settings) => {
        if (!active) return;
        applySettings(settings);
        setDirty(false);
        setReady(true);
      })
      .catch((error) => {
        if (active) setLoadError(projectOperatorError(error, t).detail);
      });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config, backend?.session, applySettings, enabled]);

  useEffect(() => load(), [load]);

  const markDirty = () => { setDirty(true); setSaved(false); };
  const withDirty = <T,>(setter: (value: T) => void) => (value: T) => { setter(value); markDirty(); };

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
      applySettings(settings);
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

  return {
    topUpEnabled, setTopUpEnabled: withDirty(setTopUpEnabled),
    topUpPercent, setTopUpPercent: withDirty(setTopUpPercent),
    shopEnabled, setShopEnabled: withDirty(setShopEnabled),
    shopPercent, setShopPercent: withDirty(setShopPercent),
    sessionEnabled, setSessionEnabled: withDirty(setSessionEnabled),
    sessionPercent, setSessionPercent: withDirty(setSessionPercent),
    cashbackCap, setCashbackCap: withDirty(setCashbackCap),
    minimumSource, setMinimumSource: withDirty(setMinimumSource),
    saveState,
    dirty,
    ready,
    loadError,
    disabled: backend === null || saving,
    save,
    retry: () => load()
  };
}
