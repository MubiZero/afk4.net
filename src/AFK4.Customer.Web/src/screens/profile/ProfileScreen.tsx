import { useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerProfileDto } from '@/api/types';
import { useToast } from '@/components/ui/toast';

interface ProfileScreenProps {
  api: PlayerApiClient;
  onSignOut: () => void;
  onLocaleChange: (locale: 'ru' | 'en') => void;
}

export function ProfileScreen({ api, onSignOut, onLocaleChange }: ProfileScreenProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [profile, setProfile] = useState<PlayerProfileDto | null>(null);
  const mountedRef = useRef(true);
  useEffect(() => () => { mountedRef.current = false; }, []);

  useEffect(() => {
    let cancelled = false;
    api.getProfile().then((p) => { if (!cancelled) setProfile(p); }).catch(() => { /* show skeleton */ });
    return () => { cancelled = true; };
  }, [api]);

  async function patch(change: { preferredLocale?: string; marketingOptIn?: boolean }) {
    try {
      const updated = await api.updateProfile(change);
      if (mountedRef.current) setProfile(updated);
      if (change.preferredLocale === 'ru' || change.preferredLocale === 'en') onLocaleChange(change.preferredLocale);
      toast({ title: t('customer.profile.saved'), variant: 'success' });
    } catch {
      toast({ title: t('customer.profile.saveError'), variant: 'error' });
    }
  }

  if (profile === null) {
    return <div role="status" aria-label={t('a11y.loading.profile')} className="m-6 h-40 animate-pulse rounded-2xl bg-[var(--color-surface)]" />;
  }

  return (
    <main className="space-y-5 px-6 py-6">
      <header>
        <h1 className="text-2xl font-extrabold tracking-tight">{profile.displayName}</h1>
        <p className="mt-1 text-sm text-[var(--text-2)]">
          {profile.phoneNumber ?? '—'} · <span className="text-[var(--text-3)]">{t('customer.profile.phoneNote')}</span>
        </p>
      </header>

      <section className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4">
        <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.profile.language')}</p>
        <div className="flex gap-2">
          {(['ru', 'en'] as const).map((locale) => (
            <button
              key={locale}
              type="button"
              onClick={() => patch({ preferredLocale: locale })}
              aria-pressed={profile.preferredLocale === locale}
              className={
                'min-h-[44px] flex-1 rounded-xl text-sm font-medium focus-visible:outline-2 focus-visible:outline-[var(--accent)] ' +
                (profile.preferredLocale === locale ? 'bg-[var(--accent)] text-[var(--accent-fg)]' : 'border border-[var(--color-border)] text-[var(--text-2)]')
              }
            >
              {locale === 'ru' ? t('customer.profile.langRu') : t('customer.profile.langEn')}
            </button>
          ))}
        </div>
      </section>

      <label className="flex items-center justify-between rounded-2xl bg-[var(--color-surface)] p-4 text-sm">
        <span>{t('customer.profile.marketing')}</span>
        <input
          type="checkbox"
          checked={profile.marketingOptIn}
          onChange={(event) => patch({ marketingOptIn: event.target.checked })}
          className="h-5 w-5 accent-[var(--accent)]"
        />
      </label>

      <button
        type="button"
        onClick={onSignOut}
        className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-red-400 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
      >
        {t('customer.profile.signOut')}
      </button>
    </main>
  );
}
