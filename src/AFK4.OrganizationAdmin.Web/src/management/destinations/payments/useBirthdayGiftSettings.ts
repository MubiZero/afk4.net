import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SaveState } from '../../ManagementScreen';
import { projectOperatorError, type OperatorErrorProjection } from '../../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../../../operatorHelpers';
import { useFeedbackToasts } from '../../../useFeedbackToasts';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';
import type { BirthdayGiftSettingsDto } from '../../../api/clients/birthdayGiftSettings';

/** Предел окна визита — зеркало `BirthdayGifts.MaxRecentVisitDays` на сервере. */
export const MAX_RECENT_VISIT_DAYS = 730;

export interface BirthdayGiftSettingsController {
  enabled: boolean; setEnabled: (value: boolean) => void;
  amount: string; setAmount: (value: string) => void;
  recentVisitDays: string; setRecentVisitDays: (value: string) => void;
  saveState: SaveState;
  dirty: boolean;
  ready: boolean;
  loadError: OperatorErrorProjection | null;
  disabled: boolean;
  save: () => Promise<void>;
  retry: () => void;
}

// Подарок на день рождения (владелец, 26.09): рядом с кэшбэком и «приведи друга», по тем же
// правилам — платит клуб, сумму назначает он же, своя кнопка сохранения внутри секции.
export function useBirthdayGiftSettings(
  backend: OperatorBackendContext | null,
  enabled = true
): BirthdayGiftSettingsController {
  const { t } = useI18n();
  const [on, setOn] = useState(false);
  const [amount, setAmount] = useState('0');
  const [recentVisitDays, setRecentVisitDays] = useState('180');
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [ready, setReady] = useState(false);
  const [loadError, setLoadError] = useState<OperatorErrorProjection | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  const applySettings = useCallback((settings: BirthdayGiftSettingsDto) => {
    setOn(settings.enabled);
    setAmount(formatMoneyInputMinorUnits(settings.amountMinorUnits));
    setRecentVisitDays(String(settings.recentVisitDays));
  }, []);

  const load = useCallback(() => {
    if (backend === null || !enabled) return undefined;
    let active = true;
    setLoadError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.birthdayGiftSettings.get()
      .then((settings) => {
        if (!active) return;
        applySettings(settings);
        setDirty(false);
        setReady(true);
      })
      .catch((error) => {
        if (active) setLoadError(projectOperatorError(error, t));
      });
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config, backend?.session, applySettings, enabled]);

  useEffect(() => load(), [load]);

  const markDirty = () => { setDirty(true); setSaved(false); };
  const withDirty = <T,>(setter: (value: T) => void) => (value: T) => { setter(value); markDirty(); };

  const save = async () => {
    if (backend === null) return;
    const label = t('op.birthdayGift.feedbackLabel');
    const amountMinor = parseNonNegativeMoneyInputMinorUnits(amount);
    if (amountMinor === null) {
      setFeedback({ label, state: 'failed', detail: t('op.birthdayGift.amountError') });
      return;
    }

    const days = Number(recentVisitDays);
    if (!Number.isInteger(days) || days < 0 || days > MAX_RECENT_VISIT_DAYS) {
      setFeedback({ label, state: 'failed', detail: t('op.birthdayGift.daysError', { max: MAX_RECENT_VISIT_DAYS }) });
      return;
    }

    // Включённый подарок в ноль — поздравление без подарка. Сервер отклонит, но сказать до
    // отправки честнее.
    if (on && amountMinor === 0) {
      setFeedback({ label, state: 'failed', detail: t('op.birthdayGift.nothingGivenError') });
      return;
    }

    setSaving(true);
    setFeedback({ label, state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const settings = await clients.birthdayGiftSettings.update({
        enabled: on,
        amountMinorUnits: amountMinor,
        recentVisitDays: days
      });
      applySettings(settings);
      setDirty(false);
      setSaved(true);
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return {
    enabled: on, setEnabled: withDirty(setOn),
    amount, setAmount: withDirty(setAmount),
    recentVisitDays, setRecentVisitDays: withDirty(setRecentVisitDays),
    saveState,
    dirty,
    ready,
    loadError,
    disabled: backend === null || saving,
    save,
    retry: () => load()
  };
}
