import { useState, type FormEvent } from 'react';
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
      setError('Неверный номер или пароль');
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col justify-center gap-8 px-6 py-12">
      <header className="space-y-1">
        <p className="text-sm text-[var(--text-2)]">Вход в портал</p>
        <h1 className="text-3xl font-extrabold tracking-tight">{brandName}</h1>
      </header>

      <form className="space-y-4" onSubmit={handleSubmit}>
        <div className="space-y-1.5">
          <label htmlFor="phone" className="text-sm text-[var(--text-2)]">Телефон</label>
          <Input id="phone" type="tel" inputMode="tel" autoComplete="tel"
            value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="+992 90 000 00 01" />
        </div>
        <div className="space-y-1.5">
          <label htmlFor="password" className="text-sm text-[var(--text-2)]">PIN или пароль</label>
          <Input id="password" type="password" autoComplete="current-password"
            value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>

        {error && <p role="alert" className="text-sm text-red-400">{error}</p>}

        <Button type="submit" className="w-full" disabled={pending}>
          {pending ? 'Входим…' : 'Войти'}
        </Button>
      </form>

      <Button type="button" variant="outline" className="w-full" disabled
        title="Вход по коду из SMS появится позже">
        Войти по SMS-коду · скоро
      </Button>
    </main>
  );
}
