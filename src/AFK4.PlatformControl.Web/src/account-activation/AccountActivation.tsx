import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import type { AccountActivationApi, AccountActivationKind } from './accountActivationApi';
import { describeApiError } from '../api/describeApiError';
import { PlatformApiError } from '../api/platformApi';
import { useI18n } from '../i18n/I18nProvider';
import type { MessageKey } from '../i18n/messages';
import { ErrorBanner, Field } from '../components/ui/field';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { BrandLogo } from '../components/shell/BrandLogo';

export interface AccountActivationProps {
  client: AccountActivationApi;
  initialCode: string | null;
  kind: AccountActivationKind;
}

// Тексты и поля расходятся: владелец заводит учётную запись для админки клуба, администратор
// платформы — для этой панели, и в списке админов его увидят коллеги, поэтому имя у него
// обязательное, а не пустая строка.
const COPY: Record<AccountActivationKind, {
  title: MessageKey;
  subtitle: MessageKey;
  submit: MessageKey;
  successTitle: MessageKey;
  successBody: MessageKey;
  needsDisplayName: boolean;
}> = {
  'organization-owner': {
    title: 'auth.accept.title',
    subtitle: 'auth.accept.subtitle',
    submit: 'auth.accept.action.submit',
    successTitle: 'auth.accept.success.title',
    successBody: 'auth.accept.success.body',
    needsDisplayName: false
  },
  'platform-admin': {
    title: 'auth.accept.platformAdmin.title',
    subtitle: 'auth.accept.platformAdmin.subtitle',
    submit: 'auth.accept.platformAdmin.action.submit',
    successTitle: 'auth.accept.platformAdmin.success.title',
    successBody: 'auth.accept.platformAdmin.success.body',
    needsDisplayName: true
  }
};

export function AccountActivation({ client, initialCode, kind }: AccountActivationProps) {
  const { t } = useI18n();
  const copy = COPY[kind];
  const [code, setCode] = useState(initialCode ?? '');
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);
  const [accepted, setAccepted] = useState(false);

  useEffect(() => setCode(initialCode ?? ''), [initialCode]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedCode = code.trim();
    const normalizedUserName = userName.trim();
    const normalizedDisplayName = displayName.trim();
    if (normalizedCode.length === 0) return setError(t('auth.accept.error.codeRequired'));
    if (normalizedUserName.length === 0) return setError(t('auth.accept.error.loginRequired'));
    if (copy.needsDisplayName && normalizedDisplayName.length === 0) return setError(t('auth.accept.error.displayNameRequired'));
    if (password.length < 8) return setError(t('auth.accept.error.passwordLength'));
    if (password !== confirmPassword) return setError(t('auth.accept.error.passwordMismatch'));

    setSubmitting(true);
    setError(null);
    try {
      await client.accept({ code: normalizedCode, userName: normalizedUserName, displayName: normalizedDisplayName, password }, kind);
      setAccepted(true);
    } catch (cause) {
      setError(describeActivationError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  if (accepted) {
    return <Frame title={t(copy.successTitle)} subtitle={t(copy.successBody)} />;
  }

  return (
    <Frame title={t(copy.title)} subtitle={t(copy.subtitle)}>
      <form className="auth-form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} dismissLabel={t('common.close')} onDismiss={() => setError(null)} />
        <Field label={t('auth.accept.field.code')} htmlFor="accept-code">
          <Input id="accept-code" autoComplete="one-time-code" value={code} onChange={event => setCode(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Field label={t('auth.field.login')} htmlFor="accept-username">
          <Input id="accept-username" autoComplete="username" value={userName} onChange={event => setUserName(event.target.value)} disabled={isSubmitting} required />
        </Field>
        {copy.needsDisplayName && (
          <Field label={t('auth.accept.field.displayName')} htmlFor="accept-display-name">
            <Input id="accept-display-name" autoComplete="name" value={displayName} onChange={event => setDisplayName(event.target.value)} disabled={isSubmitting} required />
          </Field>
        )}
        <Field label={t('auth.field.password')} htmlFor="accept-password">
          <Input id="accept-password" type="password" autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Field label={t('auth.accept.field.confirmPassword')} htmlFor="accept-confirm-password">
          <Input id="accept-confirm-password" type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? t('auth.accept.action.submitting') : t(copy.submit)}
        </Button>
      </form>
    </Frame>
  );
}

type Translate = (key: MessageKey) => string;

// Активация администратора платформы отвечает 400 и на негодный код, и на негодные данные —
// различить их можно только по коду ошибки в теле. Порядок именно такой: сначала код, потом
// статус, иначе обе причины слились бы в одну надпись «код не найден» и человек правил бы код,
// когда на самом деле короткий пароль.
function describeActivationError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode === 'invalid_details') {
    return t('auth.accept.error.detailsRejected');
  }
  return describeApiError(cause, t, {
    // 400 и 404 — разные ответы двух серверных путей на одну и ту же беду с кодом, и ни один из
    // них не уточняет, что именно не так: истёк, отозван или уже использован. Различать их вслух
    // нельзя — по разнице ответов коды можно было бы перебирать.
    400: 'auth.accept.error.codeUnusable',
    404: 'auth.accept.error.codeUnusable',
    409: 'auth.accept.error.loginTaken'
  });
}

// Активация — тот же экран-панель, что и вход: знак над заголовком, форма в приподнятой карточке.
// Раньше это была третья по счёту непохожая обёртка в одном приложении.
function Frame({ title, subtitle, children }: { title: string; subtitle: string; children?: ReactNode }) {
  const { t } = useI18n();
  return (
    <div className="pc-auth-shell">
      <header className="top-command auth-top-command">
        <div className="brand-block">
          <BrandLogo className="brand-logo" />
          <span>{t('shell.brand.section')}</span>
        </div>
      </header>
      <main className="auth-workspace">
        <section className="auth-panel">
          <header className="auth-panel-head">
            <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden="true" />
            <h1>{title}</h1>
            <p>{subtitle}</p>
          </header>
          {children}
        </section>
      </main>
    </div>
  );
}
