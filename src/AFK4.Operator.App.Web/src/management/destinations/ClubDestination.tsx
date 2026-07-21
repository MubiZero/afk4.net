import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { ClubProfileFields, type ClubProfileForm } from '../../settings/club/ClubProfileFields';
import { ClubPlayerPreview } from '../../settings/club/ClubPlayerPreview';
import { normalizeWorkingHours } from '../../settings/club/workingHours';
import { projectOperatorError } from '../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  readString,
  triggerFeedback
} from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import type { BranchProfileDto, UpdateBranchProfileRequest } from '../../api/clients/settings';
import type { Feedback } from '../../operatorTypes';
import type { DestinationProps } from './types';

const emptyForm: ClubProfileForm = {
  name: 'AFK4', city: 'Dushanbe', description: '', address: '', phone: '', telegram: '', website: '', instagram: '',
  logoUrl: null, logoMediaId: null, timeZone: 'Asia/Dushanbe', locale: 'ru', workingHours: normalizeWorkingHours(null)
};

const blankToNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

// Профиль с сервера (camelCase DTO) → форма. Один источник маппинга для загрузки и для эха после save,
// чтобы серверная нормализация (напр. форматирование телефона) доезжала до всех полей, а не только name/city.
function mapProfileToForm(profile: BranchProfileDto): ClubProfileForm {
  return {
    name: readString(profile, 'name', 'AFK4'),
    city: readString(profile, 'city', ''),
    description: readString(profile, 'description', ''),
    address: readString(profile, 'address', ''),
    phone: readString(profile, 'phone', ''),
    telegram: readString(profile, 'telegram', ''),
    website: readString(profile, 'website', ''),
    instagram: readString(profile, 'instagram', ''),
    logoUrl: (profile.logoUrl as string | null) ?? null,
    logoMediaId: (profile.logoMediaId as string | null) ?? null,
    timeZone: readString(profile, 'timeZone', 'Asia/Dushanbe'),
    locale: readString(profile, 'locale', 'ru'),
    workingHours: normalizeWorkingHours(profile.workingHours)
  };
}

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
      const request: UpdateBranchProfileRequest = {
        organizationId: backend.session.organizationId,
        name: form.name.trim(),
        city: form.city.trim(),
        description: blankToNull(form.description),
        address: blankToNull(form.address),
        phone: blankToNull(form.phone),
        telegram: blankToNull(form.telegram),
        website: blankToNull(form.website),
        instagram: blankToNull(form.instagram),
        logoUrl: form.logoUrl,
        logoMediaId: form.logoMediaId,
        timeZone: form.timeZone,
        locale: form.locale,
        workingHours: form.workingHours.map((day) => (day.isClosed
          ? { ...day, openTime: null, closeTime: null }
          : day))
      };
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
