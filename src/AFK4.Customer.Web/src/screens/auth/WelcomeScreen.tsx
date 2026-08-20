import { useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { PlayerApiClient } from '@/api/playerApi';

interface WelcomeScreenProps {
  api: PlayerApiClient;
  onDone: (displayName: string) => void;
  onLocaleChange: (locale: 'ru' | 'en') => void;
}

/**
 * Два поля, которые человек называет про себя сам, — имя и язык. PIN здесь не спрашивается: он
 * нужен позже и в ту секунду, когда человек впервые садится за ПК, а лишний шаг на входе стоит
 * дороже, чем экран «задайте PIN» в нужный момент.
 */
export function WelcomeScreen({ api, onDone, onLocaleChange }: WelcomeScreenProps) {
  const { t, locale } = useI18n();
  const [displayName, setDisplayName] = useState('');
  const [preferredLocale, setPreferredLocale] = useState<'ru' | 'en'>(locale === 'en' ? 'en' : 'ru');
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = displayName.trim();
    if (trimmed.length === 0) {
      setError(t('customer.welcome.nameError'));
      return;
    }
    setError(null);
    setPending(true);
    try {
      await api.updateMyProfile({ displayName: trimmed, preferredLocale });
      onLocaleChange(preferredLocale);
      onDone(trimmed);
    } catch {
      setError(t('customer.welcome.saveError'));
    } finally {
      setPending(false);
    }
  }

  return (
    <main className="flex min-h-dvh flex-col justify-center gap-8 px-6 py-12">
      <header className="space-y-1">
        <h1 className="text-3xl font-extrabold tracking-tight">{t('customer.welcome.title')}</h1>
        <p className="text-sm text-[var(--text-2)]">{t('customer.welcome.subtitle')}</p>
      </header>

      <form className="space-y-5" onSubmit={handleSubmit}>
        <div className="space-y-1.5">
          <label htmlFor="welcome-name" className="text-sm text-[var(--text-2)]">{t('customer.welcome.name')}</label>
          <Input id="welcome-name" type="text" autoComplete="name"
            value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </div>

        <div className="space-y-2">
          <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.profile.language')}</p>
          <div className="flex gap-2">
            {(['ru', 'en'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setPreferredLocale(option)}
                aria-pressed={preferredLocale === option}
                className={
                  'min-h-[44px] flex-1 rounded-xl text-sm font-medium focus-visible:outline-2 focus-visible:outline-[var(--accent)] ' +
                  (preferredLocale === option
                    ? 'bg-[var(--accent)] text-[var(--accent-fg)]'
                    : 'border border-[var(--color-border)] text-[var(--text-2)]')
                }
              >
                {option === 'ru' ? t('customer.profile.langRu') : t('customer.profile.langEn')}
              </button>
            ))}
          </div>
        </div>

        {error && <p role="alert" className="text-sm text-red-400">{error}</p>}

        <Button type="submit" className="w-full" disabled={pending}>
          {pending ? t('customer.welcome.saving') : t('customer.welcome.submit')}
        </Button>
      </form>
    </main>
  );
}
