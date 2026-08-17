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
import type { ReferralSettingsDto } from '../../../api/clients/referralSettings';

// Целое неотрицательное число из поля; null — введено не то, сохранять нельзя.
function toWholeCount(value: string): number | null {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0 || !Number.isInteger(parsed)) return null;
  return parsed;
}

export interface ReferralSettingsController {
  enabled: boolean; setEnabled: (value: boolean) => void;
  referrerBonus: string; setReferrerBonus: (value: string) => void;
  inviteeBonus: string; setInviteeBonus: (value: string) => void;
  minimumTopUp: string; setMinimumTopUp: (value: string) => void;
  claimWindowDays: string; setClaimWindowDays: (value: string) => void;
  maxRewardedPerReferrer: string; setMaxRewardedPerReferrer: (value: string) => void;
  saveState: SaveState;
  dirty: boolean;
  ready: boolean;
  loadError: string | null;
  disabled: boolean;
  save: () => Promise<void>;
  retry: () => void;
}

// Настройки «приведи друга». Живут рядом с кэшбэком и по тем же правилам: платит клуб, суммы
// назначает он же, своя кнопка сохранения внутри секции.
export function useReferralSettings(
  backend: OperatorBackendContext | null,
  enabled = true
): ReferralSettingsController {
  const { t } = useI18n();
  const [on, setOn] = useState(false);
  const [referrerBonus, setReferrerBonus] = useState('0');
  const [inviteeBonus, setInviteeBonus] = useState('0');
  const [minimumTopUp, setMinimumTopUp] = useState('0');
  const [claimWindowDays, setClaimWindowDays] = useState('30');
  const [maxRewardedPerReferrer, setMaxRewardedPerReferrer] = useState('0');
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [ready, setReady] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  const applySettings = useCallback((settings: ReferralSettingsDto) => {
    setOn(settings.enabled);
    setReferrerBonus(formatMoneyInputMinorUnits(settings.referrerBonusMinorUnits));
    setInviteeBonus(formatMoneyInputMinorUnits(settings.inviteeBonusMinorUnits));
    setMinimumTopUp(formatMoneyInputMinorUnits(settings.minimumTopUpMinorUnits));
    setClaimWindowDays(String(settings.claimWindowDays));
    setMaxRewardedPerReferrer(String(settings.maxRewardedPerReferrer));
  }, []);

  const load = useCallback(() => {
    if (backend === null || !enabled) return undefined;
    let active = true;
    setLoadError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.referralSettings.get()
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
    const referrerMinor = parseNonNegativeMoneyInputMinorUnits(referrerBonus);
    const inviteeMinor = parseNonNegativeMoneyInputMinorUnits(inviteeBonus);
    const minimumMinor = parseNonNegativeMoneyInputMinorUnits(minimumTopUp);
    if (referrerMinor === null || inviteeMinor === null || minimumMinor === null) {
      setFeedback({ label: t('op.referral.feedbackLabel'), state: 'failed', detail: t('op.referral.amountError') });
      return;
    }

    const windowDays = toWholeCount(claimWindowDays);
    const cap = toWholeCount(maxRewardedPerReferrer);
    if (windowDays === null || cap === null) {
      setFeedback({ label: t('op.referral.feedbackLabel'), state: 'failed', detail: t('op.referral.countError') });
      return;
    }

    // Включённая программа, которая никому не платит, — кнопка, которая ничего не делает:
    // игрок увидит экран приглашения и не получит ни дирама. Сервер это тоже отклонит, но
    // сказать об этом до отправки честнее.
    if (on && referrerMinor === 0 && inviteeMinor === 0) {
      setFeedback({ label: t('op.referral.feedbackLabel'), state: 'failed', detail: t('op.referral.nothingPaidError') });
      return;
    }

    setSaving(true);
    setFeedback({ label: t('op.referral.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const settings = await clients.referralSettings.update({
        enabled: on,
        referrerBonusMinorUnits: referrerMinor,
        inviteeBonusMinorUnits: inviteeMinor,
        minimumTopUpMinorUnits: minimumMinor,
        claimWindowDays: windowDays,
        maxRewardedPerReferrer: cap
      });
      applySettings(settings);
      setDirty(false);
      setSaved(true);
      setFeedback({ label: t('op.referral.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.referral.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return {
    enabled: on, setEnabled: withDirty(setOn),
    referrerBonus, setReferrerBonus: withDirty(setReferrerBonus),
    inviteeBonus, setInviteeBonus: withDirty(setInviteeBonus),
    minimumTopUp, setMinimumTopUp: withDirty(setMinimumTopUp),
    claimWindowDays, setClaimWindowDays: withDirty(setClaimWindowDays),
    maxRewardedPerReferrer, setMaxRewardedPerReferrer: withDirty(setMaxRewardedPerReferrer),
    saveState,
    dirty,
    ready,
    loadError,
    disabled: backend === null || saving,
    save,
    retry: () => load()
  };
}
