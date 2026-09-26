import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { AdvertiserDto } from '@/api/types';
import {
  AD_CATEGORIES,
  categoryNoteKey,
  describeCategory,
  showsPermitField,
  validateCampaignForm,
  type CampaignForm,
  type CampaignFormField
} from './adsModel';
import { useFieldErrors } from './useFieldErrors';

// Порядок полей в форме — он же порядок, в котором фокус уходит к первой ошибке.
const FIELD_IDS: Record<CampaignFormField, string> = {
  advertiserId: 'campaign-advertiser',
  name: 'campaign-name',
  category: 'campaign-category',
  permitNumber: 'campaign-permit',
  startsAt: 'campaign-starts',
  endsAt: 'campaign-ends',
  cities: 'campaign-cities',
  organizationIds: 'campaign-organizations',
  distanceSelling: 'campaign-distance-selling',
  requiresCertification: 'campaign-certification',
  containsOffer: 'campaign-offer'
};

type ComplianceFlag = 'distanceSelling' | 'requiresCertification' | 'containsOffer';

const COMPLIANCE_FLAGS: readonly { field: ComplianceFlag; labelKey: MessageKey }[] = [
  { field: 'distanceSelling', labelKey: 'platform.ads.campaign.field.distanceSelling' },
  { field: 'requiresCertification', labelKey: 'platform.ads.campaign.field.requiresCertification' },
  { field: 'containsOffer', labelKey: 'platform.ads.campaign.field.containsOffer' }
];

export function CampaignFormDialog({ mode, form, advertisers, pending, error, onChange, onSubmit, onClose }: {
  mode: 'create' | 'edit';
  form: CampaignForm;
  advertisers: readonly AdvertiserDto[];
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onChange: (form: CampaignForm) => void;
  onSubmit: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const { errorOf, controlProps, readyToSubmit } = useFieldErrors(validateCampaignForm(form), FIELD_IDS);

  function submit() {
    if (pending || !readyToSubmit()) return;
    onSubmit();
  }

  const categoryKnown = (AD_CATEGORIES as readonly string[]).includes(form.category);
  const categoryNote = categoryNoteKey(form.category);

  return (
    <Dialog
      open
      title={t(mode === 'create' ? 'platform.ads.campaign.form.createTitle' : 'platform.ads.campaign.form.editTitle')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={pending} onClick={submit}>{t('common.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <ErrorBanner message={error} dismissLabel={t('common.close')} />

        <Field label={t('platform.ads.campaign.field.advertiser')} htmlFor={FIELD_IDS.advertiserId} error={errorOf('advertiserId')}>
          <Select
            {...controlProps('advertiserId')}
            value={form.advertiserId}
            onChange={event => onChange({ ...form, advertiserId: event.target.value })}
          >
            <option value="" disabled>{t('platform.ads.campaign.field.advertiserPick')}</option>
            {advertisers.map(advertiser => (
              <option key={advertiser.advertiserId} value={advertiser.advertiserId}>{advertiser.name}</option>
            ))}
          </Select>
        </Field>

        <Field label={t('platform.ads.campaign.field.name')} htmlFor={FIELD_IDS.name} hint={t('platform.ads.campaign.field.nameHint')} error={errorOf('name')}>
          <Input {...controlProps('name')} value={form.name} onChange={event => onChange({ ...form, name: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.campaign.field.category')}
          htmlFor={FIELD_IDS.category}
          hint={t('platform.ads.campaign.field.categoryHint')}
          error={errorOf('category')}
        >
          <Select
            {...controlProps('category')}
            value={form.category}
            onChange={event => onChange({ ...form, category: event.target.value as CampaignForm['category'] })}
          >
            {/* Категорию, которой этот экран не знает, показываем как есть: подмена молча переписала бы её. */}
            {categoryKnown ? null : <option value={form.category}>{form.category}</option>}
            {AD_CATEGORIES.map(category => (
              <option key={category} value={category}>{describeCategory(category, t)}</option>
            ))}
          </Select>
        </Field>
        {categoryNote !== null ? <p className="mgmt-drawer-hint">{t(categoryNote)}</p> : null}

        {showsPermitField(form) ? (
          <Field
            label={t('platform.ads.campaign.field.permit')}
            htmlFor={FIELD_IDS.permitNumber}
            hint={t('platform.ads.campaign.field.permitHint')}
            error={errorOf('permitNumber')}
          >
            <Input
              {...controlProps('permitNumber')}
              autoComplete="off"
              spellCheck={false}
              value={form.permitNumber}
              onChange={event => onChange({ ...form, permitNumber: event.target.value })}
            />
          </Field>
        ) : null}

        <div className="mgmt-form-grid">
          <Field label={t('platform.ads.campaign.field.startsAt')} htmlFor={FIELD_IDS.startsAt} error={errorOf('startsAt')}>
            <Input
              {...controlProps('startsAt')}
              type="datetime-local"
              value={form.startsAt}
              onChange={event => onChange({ ...form, startsAt: event.target.value })}
            />
          </Field>
          <Field label={t('platform.ads.campaign.field.endsAt')} htmlFor={FIELD_IDS.endsAt} error={errorOf('endsAt')}>
            <Input
              {...controlProps('endsAt')}
              type="datetime-local"
              value={form.endsAt}
              onChange={event => onChange({ ...form, endsAt: event.target.value })}
            />
          </Field>
        </div>
        <p className="mgmt-drawer-hint">{t('platform.ads.campaign.field.datesHint')}</p>

        <Field label={t('platform.ads.campaign.field.cities')} htmlFor={FIELD_IDS.cities} hint={t('platform.ads.campaign.field.citiesHint')} error={errorOf('cities')}>
          <Input {...controlProps('cities')} value={form.cities} onChange={event => onChange({ ...form, cities: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.campaign.field.organizations')}
          htmlFor={FIELD_IDS.organizationIds}
          hint={t('platform.ads.campaign.field.organizationsHint')}
          error={errorOf('organizationIds')}
        >
          <Textarea
            {...controlProps('organizationIds')}
            rows={3}
            className="pc-mono"
            spellCheck={false}
            autoComplete="off"
            value={form.organizationIds}
            onChange={event => onChange({ ...form, organizationIds: event.target.value })}
          />
        </Field>

        {/* Закон требует от карточки сказать это самой (спека рекламы, §8.1): отметка — и ПК допишет. */}
        <fieldset className="pc-ad-compliance">
          <legend>{t('platform.ads.campaign.compliance.legend')}</legend>
          {COMPLIANCE_FLAGS.map(({ field, labelKey }) => (
            <label key={field} className="mgmt-check">
              <input
                id={FIELD_IDS[field]}
                type="checkbox"
                checked={form[field]}
                onChange={event => onChange({ ...form, [field]: event.target.checked })}
              />
              {t(labelKey)}
            </label>
          ))}
        </fieldset>
      </div>
    </Dialog>
  );
}
