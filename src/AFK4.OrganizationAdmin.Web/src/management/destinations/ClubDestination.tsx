import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { ClubProfileFields, type ClubProfileForm } from '../../settings/club/ClubProfileFields';
import { ClubPlayerPreview } from '../../settings/club/ClubPlayerPreview';
import { normalizeWorkingHours } from '../../settings/club/workingHours';
import { mapProfileToForm, buildUpdateBranchProfileRequest } from '../../settings/club/branchProfileRequest';
import { projectOperatorError } from '../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  triggerFeedback
} from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import type { BranchProfileDto } from '../../api/clients/settings';
import type { Feedback } from '../../operatorTypes';
import type { DestinationProps } from './types';

const emptyForm: ClubProfileForm = {
  name: 'AFK4', city: 'Dushanbe', description: '', address: '', phone: '', telegram: '', website: '', instagram: '',
  logoUrl: null, logoMediaId: null, coverImageUrl: null, coverMediaId: null, latitude: '', longitude: '',
  timeZone: 'Asia/Dushanbe', locale: 'ru', workingHours: normalizeWorkingHours(null)
};

// Клуб: полный профиль филиала (лицо игрока + контакты + часы + настройки). Название — человекочитаемое,
// НИКОГДА не UUID. Гейт раздела — manageBranchSettings (managementNav); эндпоинт profile — то же право.
export function ClubDestination({ backend, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const [form, setForm] = useState<ClubProfileForm>(emptyForm);
  // Снимок последнего загруженного/сохранённого профиля — база для «Отменить».
  const [baseline, setBaseline] = useState<ClubProfileForm>(emptyForm);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    if (backend === null) return undefined;
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.settings.getBranchProfile(backend.branchId)
      .then((profile) => {
        if (!active) return;
        const mapped = mapProfileToForm(profile);
        setForm(mapped);
        setBaseline(mapped);
        setDirty(false);
      })
      .catch((error) => {
        if (!active) return;
        setFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });
    return () => { active = false; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);

  const onField = <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    if (!form.name.trim() || !form.city.trim()) {
      triggerFeedback(setFeedback, t('op.settings.profile.feedbackLabel'), 'failed', t('op.settings.profile.errorRequiredFields'));
      return;
    }
    setSaving(true);
    setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const request = buildUpdateBranchProfileRequest(backend.session.organizationId, form);
      const profile: BranchProfileDto = await clients.settings.updateBranchProfile(backend.branchId, request);
      const mapped = mapProfileToForm(profile);
      setForm(mapped);
      setBaseline(mapped);
      setDirty(false);
      setSaved(true);
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const discard = () => {
    setForm(baseline);
    setDirty(false);
    setSaved(false);
    setFeedback(emptyFeedback);
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return (
    <ManagementScreen
      title={t('op.management.dest.club')}
      subtitle={t('op.management.dest.club.subtitle')}
      contentWidth="full"
      save={{ state: saveState, onSave: () => void save(), onDiscard: discard, disabled: backend === null }}
    >
      <div className="club-profile-layout">
        {backend !== null && (
          <ClubProfileFields
            form={form}
            currencyCode={currencyCode}
            backend={backend}
            disabled={saving}
            onField={onField}
            preview={<ClubPlayerPreview form={form} />}
          />
        )}
      </div>
    </ManagementScreen>
  );
}
