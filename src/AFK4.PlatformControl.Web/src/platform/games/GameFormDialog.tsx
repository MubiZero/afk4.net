import { useRef, useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field, fieldErrorId } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import {
  LAUNCH_KINDS,
  ageOptions,
  describeLaunchKind,
  formatAgeMark,
  hasErrors,
  isHttpsUrl,
  launchTargetHintKey,
  launchTargetLabelKey,
  validateGameForm,
  type GameForm,
  type GameFormField
} from './gamesModel';

interface Props {
  mode: 'create' | 'edit';
  form: GameForm;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onChange: (form: GameForm) => void;
  onSubmit: () => void;
  onClose: () => void;
  /** Картинка магазина Steam по номеру приложения; отказ — уже человеческой фразой. */
  onSteamCover: (steamAppId: string) => Promise<string>;
  /** Своя картинка в хранилище платформы; возвращает её адрес. */
  onUploadCover: (file: File) => Promise<string>;
}

// Порядок полей в форме — он же порядок, в котором фокус уходит к первой ошибке.
const FIELD_IDS: Record<GameFormField, string> = {
  name: 'game-name',
  description: 'game-description',
  genre: 'game-genre',
  launchTarget: 'game-launch-target',
  coverUrl: 'game-cover'
};

export function GameFormDialog({ mode, form, pending, error, onChange, onSubmit, onClose, onSteamCover, onUploadCover }: Props) {
  const { t } = useI18n();
  // Ошибку поля показываем, когда человек из него ушёл или попробовал сохранить: красное
  // «укажите название» на только что открытой пустой форме — упрёк за то, чего он ещё не делал.
  const [touched, setTouched] = useState<ReadonlySet<GameFormField>>(new Set());
  const [attempted, setAttempted] = useState(false);
  const [brokenCover, setBrokenCover] = useState<string | null>(null);
  const [coverBusy, setCoverBusy] = useState(false);
  const [coverError, setCoverError] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const errors = validateGameForm(form);
  const errorOf = (field: GameFormField): string | undefined => {
    const found = errors[field];
    return found !== undefined && (attempted || touched.has(field)) ? t(found.key, found.values) : undefined;
  };
  const controlProps = (field: GameFormField) => {
    const message = errorOf(field);
    return {
      id: FIELD_IDS[field],
      'aria-invalid': message !== undefined ? true : undefined,
      'aria-describedby': message !== undefined ? fieldErrorId(FIELD_IDS[field]) : undefined,
      onBlur: () => setTouched(previous => new Set(previous).add(field))
    };
  };

  function submit() {
    if (pending) return;
    if (hasErrors(errors)) {
      setAttempted(true);
      const first = (Object.keys(FIELD_IDS) as GameFormField[]).find(field => errors[field] !== undefined);
      if (first !== undefined) document.getElementById(FIELD_IDS[first])?.focus();
      return;
    }
    onSubmit();
  }

  const coverUrl = form.coverUrl.trim();
  const showCover = isHttpsUrl(coverUrl);
  const steamAppId = form.launchKind === 'steam' && /^\d{1,10}$/.test(form.launchTarget.trim()) ? form.launchTarget.trim() : null;

  // Картинку ставим в поле только после ответа сервера: адрес — уже в нашем хранилище.
  async function fetchCover(load: () => Promise<string>) {
    setCoverBusy(true);
    setCoverError(null);
    try {
      const url = await load();
      onChange({ ...form, coverUrl: url });
    } catch (cause) {
      setCoverError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setCoverBusy(false);
    }
  }

  return (
    <Dialog
      open
      title={mode === 'create' ? t('platform.games.form.createTitle') : t('platform.games.form.editTitle')}
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

        <Field label={t('platform.games.field.name')} htmlFor={FIELD_IDS.name} error={errorOf('name')}>
          <Input {...controlProps('name')} value={form.name} onChange={event => onChange({ ...form, name: event.target.value })} />
        </Field>

        <Field label={t('platform.games.field.description')} htmlFor={FIELD_IDS.description} error={errorOf('description')}>
          <Textarea
            {...controlProps('description')}
            rows={3}
            value={form.description}
            onChange={event => onChange({ ...form, description: event.target.value })}
          />
        </Field>

        <div className="mgmt-form-grid">
          <Field label={t('platform.games.field.genre')} htmlFor={FIELD_IDS.genre} error={errorOf('genre')}>
            <Input {...controlProps('genre')} value={form.genre} onChange={event => onChange({ ...form, genre: event.target.value })} />
          </Field>
          <Field label={t('platform.games.field.age')} htmlFor="game-age">
            <Select id="game-age" value={form.minAge} onChange={event => onChange({ ...form, minAge: event.target.value })}>
              <option value="">{t('platform.games.field.ageNone')}</option>
              {ageOptions(form.minAge).map(age => (
                <option key={age} value={String(age)}>{formatAgeMark(age)}</option>
              ))}
            </Select>
          </Field>
        </div>
        <p className="mgmt-drawer-hint">{t('platform.games.field.ageHint')}</p>

        <Field label={t('platform.games.field.launchKind')} htmlFor="game-launch-kind" hint={t('platform.games.field.launchKindHint')}>
          <Select
            id="game-launch-kind"
            value={form.launchKind}
            onChange={event => onChange({ ...form, launchKind: event.target.value as GameForm['launchKind'] })}
          >
            {LAUNCH_KINDS.map(kind => (
              <option key={kind} value={kind}>{describeLaunchKind(kind, t)}</option>
            ))}
          </Select>
        </Field>

        <Field
          label={t(launchTargetLabelKey(form.launchKind))}
          htmlFor={FIELD_IDS.launchTarget}
          hint={t(launchTargetHintKey(form.launchKind))}
          error={errorOf('launchTarget')}
        >
          <Input
            {...controlProps('launchTarget')}
            className="pc-mono"
            spellCheck={false}
            autoComplete="off"
            value={form.launchTarget}
            onChange={event => onChange({ ...form, launchTarget: event.target.value })}
          />
        </Field>

        <Field label={t('platform.games.field.cover')} htmlFor={FIELD_IDS.coverUrl} hint={t('platform.games.field.coverHint')} error={errorOf('coverUrl')}>
          <Input
            {...controlProps('coverUrl')}
            type="url"
            inputMode="url"
            placeholder="https://"
            spellCheck={false}
            value={form.coverUrl}
            onChange={event => onChange({ ...form, coverUrl: event.target.value })}
          />
        </Field>
        <div className="pc-cover-actions">
          {steamAppId !== null ? (
            <Button variant="outline" size="sm" disabled={pending || coverBusy} onClick={() => void fetchCover(() => onSteamCover(steamAppId))}>
              {t('platform.games.cover.fromSteam')}
            </Button>
          ) : null}
          <Button variant="outline" size="sm" disabled={pending || coverBusy} onClick={() => fileInput.current?.click()}>
            {t('platform.media.upload')}
          </Button>
          <input
            ref={fileInput}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            hidden
            aria-label={t('platform.media.upload')}
            onChange={event => {
              const file = event.currentTarget.files?.[0];
              event.currentTarget.value = '';
              if (file) void fetchCover(() => onUploadCover(file));
            }}
          />
          {coverBusy ? <span className="mgmt-drawer-hint">{t('platform.media.loading')}</span> : null}
        </div>
        {coverError !== null ? <p className="pc-error-text" role="alert">{coverError}</p> : null}
        {showCover && brokenCover !== coverUrl ? (
          <img
            className="pc-game-cover-preview"
            src={coverUrl}
            alt={t('platform.games.field.coverPreview')}
            onError={() => setBrokenCover(coverUrl)}
          />
        ) : null}
        {showCover && brokenCover === coverUrl ? <p className="pc-error-text">{t('platform.games.field.coverBroken')}</p> : null}

        <label className="pc-check-row">
          <Switch checked={form.isPublished} onCheckedChange={checked => onChange({ ...form, isPublished: checked })} />
          {t('platform.games.field.publish')}
        </label>
        <p className="mgmt-drawer-hint">{t('platform.games.field.publishHint')}</p>
      </div>
    </Dialog>
  );
}
