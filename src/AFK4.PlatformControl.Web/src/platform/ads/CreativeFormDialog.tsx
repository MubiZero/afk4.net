import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import { AD_LIMITS, IMAGE_MAX_MB, validateCreativeForm, type CreativeForm, type CreativeFormField } from './adsModel';
import { AdImagePreview } from './AdImages';
import { useFieldErrors } from './useFieldErrors';

// Порядок полей в форме — он же порядок, в котором фокус уходит к первой ошибке.
const FIELD_IDS: Record<CreativeFormField, string> = {
  title: 'creative-title',
  body: 'creative-body',
  titleRu: 'creative-title-ru',
  bodyRu: 'creative-body-ru',
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

  const count = (value: string, max: number) => t('platform.ads.creative.field.count', { count: value.trim().length, max });

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
        <p className="mgmt-drawer-hint">{t('platform.ads.creative.form.languageHint')}</p>

        <Field label={t('platform.ads.creative.field.titleTg')} htmlFor={FIELD_IDS.title} hint={count(form.title, AD_LIMITS.titleMax)} error={errorOf('title')}>
          <Input {...controlProps('title')} lang="tg" value={form.title} onChange={event => onChange({ ...form, title: event.target.value })} />
        </Field>

        <Field label={t('platform.ads.creative.field.bodyTg')} htmlFor={FIELD_IDS.body} hint={count(form.body, AD_LIMITS.bodyMax)} error={errorOf('body')}>
          <Textarea {...controlProps('body')} lang="tg" rows={3} value={form.body} onChange={event => onChange({ ...form, body: event.target.value })} />
        </Field>

        <Field label={t('platform.ads.creative.field.titleRu')} htmlFor={FIELD_IDS.titleRu} hint={count(form.titleRu, AD_LIMITS.titleMax)} error={errorOf('titleRu')}>
          <Input {...controlProps('titleRu')} lang="ru" value={form.titleRu} onChange={event => onChange({ ...form, titleRu: event.target.value })} />
        </Field>

        <Field label={t('platform.ads.creative.field.bodyRu')} htmlFor={FIELD_IDS.bodyRu} hint={count(form.bodyRu, AD_LIMITS.bodyMax)} error={errorOf('bodyRu')}>
          <Textarea {...controlProps('bodyRu')} lang="ru" rows={3} value={form.bodyRu} onChange={event => onChange({ ...form, bodyRu: event.target.value })} />
        </Field>

        <Field
          label={t('platform.ads.creative.field.image')}
          htmlFor={FIELD_IDS.imageUrl}
          hint={t('platform.ads.creative.field.imageHint', { mb: IMAGE_MAX_MB })}
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
