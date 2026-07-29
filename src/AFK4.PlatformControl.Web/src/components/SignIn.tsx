import { useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';
import { Button } from './ui/button';
import { Card, CardContent, CardHeader, CardTitle } from './ui/card';
import { Input } from './ui/input';

export interface SignInProps {
  client: PlatformApiClient;
  onSignedIn: () => void;
}

export function SignIn({ client, onSignedIn }: SignInProps) {
  const { t } = useI18n();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await client.signIn(userName.trim(), password);
      onSignedIn();
    } catch (cause) {
      if (cause instanceof PlatformApiError) {
        setError(cause.status === 401 ? t('auth.error.invalid') : cause.message);
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError(t('auth.error.generic'));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="grid min-h-screen place-items-center bg-background p-5">
      <Card className="w-full max-w-md">
      <CardHeader><CardTitle><h1>{t('auth.admin.title')}</h1></CardTitle><p className="text-sm text-muted-foreground">{t('auth.admin.subtitle')}</p></CardHeader>
      <CardContent><form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <ErrorBanner message={error} dismissLabel={t('common.close')} onDismiss={() => setError(null)} />
        <Field label={t('auth.field.login')} htmlFor="signin-username">
          <Input
            id="signin-username"
            name="userName"
            type="text"
            autoComplete="username"
            value={userName}
            onChange={event => setUserName(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.field.password')} htmlFor="signin-password">
          <Input
            id="signin-password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? t('auth.action.signingIn') : t('auth.action.signIn')}
        </Button>
      </form></CardContent>
      </Card>
    </main>
  );
}
