import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { AD_LIMITS, validateAdvertiserForm, type AdvertiserForm, type AdvertiserFormField } from './adsModel';
import { useFieldErrors } from './useFieldErrors';

// Порядок полей в форме — он же порядок, в котором фокус уходит к первой ошибке.
const FIELD_IDS: Record<AdvertiserFormField, string> = {
  name: 'advertiser-name',
  legalName: 'advertiser-legal-name',
  taxId: 'advertiser-tax-id',
  address: 'advertiser-address',
  contact: 'advertiser-contact'
};

export function AdvertiserFormDialog({ mode, form, pending, error, onChange, onSubmit, onClose }: {
  mode: 'create' | 'edit';
  form: AdvertiserForm;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onChange: (form: AdvertiserForm) => void;
  onSubmit: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const { errorOf, controlProps, readyToSubmit } = useFieldErrors(validateAdvertiserForm(form), FIELD_IDS);

  function submit() {
    if (pending || !readyToSubmit()) return;
    onSubmit();
  }

  return (
    <Dialog
      open
      title={t(mode === 'create' ? 'platform.ads.advertiser.form.createTitle' : 'platform.ads.advertiser.form.editTitle')}
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

        <Field
          label={t('platform.ads.advertiser.field.name')}
          htmlFor={FIELD_IDS.name}
          hint={t('platform.ads.advertiser.field.nameHint')}
          error={errorOf('name')}
        >
          <Input {...controlProps('name')} value={form.name} onChange={event => onChange({ ...form, name: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.advertiser.field.legalName')}
          htmlFor={FIELD_IDS.legalName}
          hint={t('platform.ads.advertiser.field.legalNameHint')}
          error={errorOf('legalName')}
        >
          <Input
            {...controlProps('legalName')}
            autoComplete="organization"
            value={form.legalName}
            onChange={event => onChange({ ...form, legalName: event.target.value })}
          />
        </Field>

        <Field
          label={t('platform.ads.advertiser.field.taxId')}
          htmlFor={FIELD_IDS.taxId}
          hint={t('platform.ads.advertiser.field.taxIdHint', { min: AD_LIMITS.taxIdMinDigits, max: AD_LIMITS.taxIdMaxDigits })}
          error={errorOf('taxId')}
        >
          <Input
            {...controlProps('taxId')}
            className="pc-mono"
            inputMode="numeric"
            autoComplete="off"
            spellCheck={false}
            value={form.taxId}
            onChange={event => onChange({ ...form, taxId: event.target.value })}
          />
        </Field>

        <Field
          label={t('platform.ads.advertiser.field.address')}
          htmlFor={FIELD_IDS.address}
          hint={t('platform.ads.advertiser.field.addressHint')}
          error={errorOf('address')}
        >
          <Input
            {...controlProps('address')}
            autoComplete="street-address"
            value={form.address}
            onChange={event => onChange({ ...form, address: event.target.value })}
          />
        </Field>

        <Field
          label={t('platform.ads.advertiser.field.contact')}
          htmlFor={FIELD_IDS.contact}
          hint={t('platform.ads.advertiser.field.contactHint')}
          error={errorOf('contact')}
        >
          <Textarea
            {...controlProps('contact')}
            rows={3}
            value={form.contact}
            onChange={event => onChange({ ...form, contact: event.target.value })}
          />
        </Field>
      </div>
    </Dialog>
  );
}
