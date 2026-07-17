import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { SettingsProfileSection } from '../../settings/SettingsProfileSection';
import { projectOperatorError } from '../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  readString,
  triggerFeedback
} from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import type { BranchProfileDto } from '../../operatorApiClients';
import type { Feedback } from '../../operatorTypes';
import type { DestinationProps } from './types';

// Клуб: профиль филиала (имя человекочитаемое, НИКОГДА не UUID — см. constraint). Загрузка/save
// перенесены сюда 1:1 из BackendSettingsWorkspace.saveSettings — тот же API-контракт
// (settings.getBranchProfile/updateBranchProfile), тот же feedback-канал через тосты.
export function ClubDestination({ backend, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const [clubName, setClubName] = useState('AFK4');
  const [city, setCity] = useState('Dushanbe');
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    if (backend === null) {
      return undefined;
    }

    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.settings.getBranchProfile(backend.branchId)
      .then((profile) => {
        if (!active) return;
        setClubName(readString(profile, 'name', 'AFK4'));
        setCity(readString(profile, 'city', 'Dushanbe'));
        setDirty(false);
      })
      .catch((error) => {
        if (!active) return;
        setFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });
    return () => { active = false; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => {
    onDirtyChange?.(dirty);
  }, [dirty, onDirtyChange]);

  const markDirty = () => {
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, t('op.settings.profile.feedbackLabel'), 'failed', t('op.settings.profile.errorRequiredFields'));
      return;
    }

    setSaving(true);
    setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const profile: BranchProfileDto = await clients.settings.updateBranchProfile(backend.branchId, {
        organizationId: backend.session.organizationId,
        name: clubName.trim(),
        city: city.trim()
      });
      setClubName(readString(profile, 'name', clubName.trim()));
      setCity(readString(profile, 'city', city.trim()));
      setDirty(false);
      setSaved(true);
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return (
    <ManagementScreen
      title={t('op.management.dest.club')}
      subtitle={t('op.management.dest.club.subtitle')}
      save={{ state: saveState, onSave: () => void save(), disabled: backend === null }}
    >
      <div className="management-panel">
        <SettingsProfileSection
          clubName={clubName}
          city={city}
          currencyCode={currencyCode}
          hasBackend={backend !== null}
          onClubNameChange={(value) => { setClubName(value); markDirty(); }}
          onCityChange={(value) => { setCity(value); markDirty(); }}
        />
      </div>
    </ManagementScreen>
  );
}
