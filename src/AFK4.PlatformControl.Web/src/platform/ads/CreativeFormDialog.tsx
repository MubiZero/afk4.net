import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { AD_LIMITS, validateCreativeForm, type CreativeForm, type CreativeFormField } from './adsModel';
import { AdImagePreview } from './AdImages';
import { useFieldErrors } from './useFieldErrors';

const FIELD_IDS: Record<CreativeFormField, string> = {
  title: 'creative-title',
  body: 'creative-body',
  imageUrl: 'creative-image'
};

export function CreativeFormDialog({ mode, form, pending, error, onChange, onSubmit, onClose }: {
  mode: 'create' | 'edit';
  form: CreativeForm;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onChange: (form: CreativeForm) => void;
  onSubmit: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const { errorOf, controlProps, readyToSubmit } = useFieldErrors(validateCreativeForm(form), FIELD_IDS);

  function submit() {
    if (pending || !readyToSubmit()) return;
    onSubmit();
  }

  return (
    <Dialog
      open
      title={t(mode === 'create' ? 'platform.ads.creative.form.createTitle' : 'platform.ads.creative.form.editTitle')}
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
        {/* Правка снимает одобрение — сказать это нужно до «Сохранить», а не тостом после. */}
        <p className="mgmt-drawer-hint">
          {t(mode === 'create' ? 'platform.ads.creative.form.newHint' : 'platform.ads.creative.form.remoderateHint')}
        </p>

        <Field
          label={t('platform.ads.creative.field.title')}
          htmlFor={FIELD_IDS.title}
          hint={t('platform.ads.creative.field.count', { count: form.title.trim().length, max: AD_LIMITS.titleMax })}
          error={errorOf('title')}
        >
          <Input {...controlProps('title')} value={form.title} onChange={event => onChange({ ...form, title: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.creative.field.body')}
          htmlFor={FIELD_IDS.body}
          hint={t('platform.ads.creative.field.count', { count: form.body.trim().length, max: AD_LIMITS.bodyMax })}
          error={errorOf('body')}
        >
          <Textarea {...controlProps('body')} rows={3} value={form.body} onChange={event => onChange({ ...form, body: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.creative.field.image')}
          htmlFor={FIELD_IDS.imageUrl}
          hint={t('platform.ads.creative.field.imageHint')}
          error={errorOf('imageUrl')}
        >
          <Input
            {...controlProps('imageUrl')}
            type="url"
            inputMode="url"
            placeholder="https://"
            spellCheck={false}
            value={form.imageUrl}
            onChange={event => onChange({ ...form, imageUrl: event.target.value })}
          />
        </Field>
        <AdImagePreview url={form.imageUrl} />
      </div>
    </Dialog>
  );
}
