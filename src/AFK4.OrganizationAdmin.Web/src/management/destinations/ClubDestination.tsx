import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { ClubProfileFields, type ClubBrandForm, type ClubProfileForm } from '../../settings/club/ClubProfileFields';
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
  logoUrl: null, logoMediaId: null, coverImageUrl: null, coverMediaId: null, photos: [], latitude: '', longitude: '',
  timeZone: 'Asia/Dushanbe', locale: 'ru', workingHours: normalizeWorkingHours(null)
};

const emptyBrand: ClubBrandForm = { logoUrl: null, accentColor: null };

// Клуб: полный профиль филиала (лицо игрока + контакты + часы + настройки). Название — человекочитаемое,
// НИКОГДА не UUID. Гейт раздела — manageBranchSettings (managementNav); эндпоинт profile — то же право.
export function ClubDestination({ backend, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const [form, setForm] = useState<ClubProfileForm>(emptyForm);
  // Снимок последнего загруженного/сохранённого профиля — база для «Отменить».
  const [baseline, setBaseline] = useState<ClubProfileForm>(emptyForm);
  const [brand, setBrand] = useState<ClubBrandForm>(emptyBrand);
  const [brandBaseline, setBrandBaseline] = useState<ClubBrandForm>(emptyBrand);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    if (backend === null) return undefined;
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    // Профиль филиала и оформление сети приходят разными запросами, но на экране это одна
    // страница клуба: показывать её без бренда значило бы открыть форму пустой и затереть
    // выбранное в мастере установки.
    Promise.all([
      clients.settings.getBranchProfile(backend.branchId),
      clients.branding.getBranding()
    ])
      .then(([profile, branding]) => {
        if (!active) return;
        const mapped = mapProfileToForm(profile);
        const loadedBrand: ClubBrandForm = { logoUrl: branding.logoUrl, accentColor: branding.accentColor };
        setForm(mapped);
        setBaseline(mapped);
        setBrand(loadedBrand);
        setBrandBaseline(loadedBrand);
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

  const onBrandField = <K extends keyof ClubBrandForm>(key: K, value: ClubBrandForm[K]) => {
    setBrand((prev) => ({ ...prev, [key]: value }));
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
      // Оформление сети трогаем, только если его меняли: лишний PATCH писал бы в журнал клуба
      // событие про бренд каждый раз, когда правят телефон филиала.
      const brandChanged = brand.logoUrl !== brandBaseline.logoUrl || brand.accentColor !== brandBaseline.accentColor;
      const savedBrand = brandChanged ? await clients.branding.updateBranding(brand) : null;
      const nextBrand: ClubBrandForm = savedBrand === null
        ? brand
        : { logoUrl: savedBrand.logoUrl, accentColor: savedBrand.accentColor };
      const mapped = mapProfileToForm(profile);
      setForm(mapped);
      setBaseline(mapped);
      setBrand(nextBrand);
      setBrandBaseline(nextBrand);
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
    setBrand(brandBaseline);
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
            brand={brand}
            currencyCode={currencyCode}
            backend={backend}
            disabled={saving}
            onField={onField}
            onBrandField={onBrandField}
            preview={<ClubPlayerPreview form={form} />}
          />
        )}
      </div>
    </ManagementScreen>
  );
}
