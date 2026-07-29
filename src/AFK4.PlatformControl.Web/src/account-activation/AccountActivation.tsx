import { useEffect, useState, type FormEvent } from 'react';
import type { AccountActivationApi } from './accountActivationApi';
import { PlatformApiError } from '../api/platformApi';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from '../components/ui';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { Input } from '../components/ui/input';

export interface AccountActivationProps {
  client: AccountActivationApi;
  initialCode: string | null;
}

export function AccountActivation({ client, initialCode }: AccountActivationProps) {
  const { t } = useI18n();
  const [code, setCode] = useState(initialCode ?? '');
  const [userName, setUserName] = useState('');
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
    if (normalizedCode.length === 0) return setError(t('auth.accept.error.codeRequired'));
    if (normalizedUserName.length === 0) return setError(t('auth.accept.error.loginRequired'));
    if (password.length < 8) return setError(t('auth.accept.error.passwordLength'));
    if (password !== confirmPassword) return setError(t('auth.accept.error.passwordMismatch'));

    setSubmitting(true);
    setError(null);
    try {
      await client.accept({ code: normalizedCode, userName: normalizedUserName, displayName: '', password });
      setAccepted(true);
    } catch (cause) {
      setError(projectError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  if (accepted) {
    return (
      <main className="grid min-h-screen place-items-center bg-background p-5"><Card className="w-full max-w-md"><CardHeader><CardTitle><h1>{t('auth.accept.success.title')}</h1></CardTitle></CardHeader><CardContent><p className="text-sm text-muted-foreground">{t('auth.accept.success.body')}</p></CardContent></Card></main>
    );
  }

  return (
    <main className="grid min-h-screen place-items-center bg-background p-5"><Card className="w-full max-w-md">
      <CardHeader><CardTitle><h1>{t('auth.accept.title')}</h1></CardTitle><p className="text-sm text-muted-foreground">{t('auth.accept.subtitle')}</p></CardHeader>
      <CardContent><form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <ErrorBanner message={error} dismissLabel={t('common.close')} onDismiss={() => setError(null)} />
        <Field label={t('auth.accept.field.code')} htmlFor="accept-code">
          <Input id="accept-code" autoComplete="one-time-code" value={code} onChange={event => setCode(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Field label={t('auth.field.login')} htmlFor="accept-username">
          <Input id="accept-username" autoComplete="username" value={userName} onChange={event => setUserName(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Field label={t('auth.field.password')} htmlFor="accept-password">
          <Input id="accept-password" type="password" autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Field label={t('auth.accept.field.confirmPassword')} htmlFor="accept-confirm-password">
          <Input id="accept-confirm-password" type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} disabled={isSubmitting} required />
        </Field>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? t('auth.accept.action.submitting') : t('auth.accept.action.submit')}
        </Button>
      </form></CardContent>
    </Card></main>
  );
}

function projectError(cause: unknown, t: ReturnType<typeof useI18n>['t']): string {
  if (cause instanceof PlatformApiError) {
    if (cause.status === 404) return t('auth.accept.error.codeNotFound');
    if (cause.status === 409) return t('auth.accept.error.loginTaken');
    return cause.message;
  }
  return cause instanceof Error ? cause.message : t('auth.accept.error.generic');
}
