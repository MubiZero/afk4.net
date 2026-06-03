import { useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { PlayerSignInRequest, PlayerSignInResponse } from '@/api/types';

interface SignInScreenProps {
  organizationId: string;
  brandName: string;
  signIn: (request: PlayerSignInRequest) => Promise<PlayerSignInResponse>;
  onSignedIn: (response: PlayerSignInResponse) => void;
}

export function SignInScreen({ organizationId, brandName, signIn, onSignedIn }: SignInScreenProps) {
  const { t } = useI18n();
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      const response = await signIn({ organizationId, phoneNumber, password });
      onSignedIn(response);
    } catch {
      setError(t('customer.signin.error'));
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col justify-center gap-8 px-6 py-12">
      <header className="space-y-1">
        <p className="text-sm text-[var(--text-2)]">{t('customer.signin.title')}</p>
        <h1 className="text-3xl font-extrabold tracking-tight">{brandName}</h1>
      </header>

      <form className="space-y-4" onSubmit={handleSubmit}>
        <div className="space-y-1.5">
          <label htmlFor="phone" className="text-sm text-[var(--text-2)]">{t('customer.signin.phone')}</label>
          <Input id="phone" type="tel" inputMode="tel" autoComplete="tel"
            value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="+992 90 000 00 01" />
        </div>
        <div className="space-y-1.5">
          <label htmlFor="password" className="text-sm text-[var(--text-2)]">{t('customer.signin.password')}</label>
          <Input id="password" type="password" autoComplete="current-password"
            value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>

        {error && <p role="alert" className="text-sm text-red-400">{error}</p>}

        <Button type="submit" className="w-full" disabled={pending}>
          {pending ? t('customer.signin.submitting') : t('customer.signin.submit')}
        </Button>
      </form>

      <Button type="button" variant="outline" className="w-full" disabled
        title={t('customer.signin.otpSoon')}>
        {t('customer.signin.otpSoon')}
      </Button>
    </main>
  );
}
