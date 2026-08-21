import { useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { MePersonDto, PlayerProfileDto } from '@/api/types';
import { useToast } from '@/components/ui/toast';
import { PinPanel } from './PinPanel';

interface ProfileScreenProps {
  api: PlayerApiClient;
  /** Личность: имя, номер и признак PIN принадлежат человеку, а не счёту в клубе. */
  person: MePersonDto | null;
  onPersonChanged: () => void;
  onSignOut: () => void;
  onLocaleChange: (locale: 'ru' | 'en') => void;
}

export function ProfileScreen({ api, person, onPersonChanged, onSignOut, onLocaleChange }: ProfileScreenProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  // Клубная часть профиля: она есть только там, где у человека открыт счёт. У того, кто в этот
  // клуб ещё не заходил, её нет — и это нормальное состояние, а не сбой загрузки.
  const [clubProfile, setClubProfile] = useState<PlayerProfileDto | null>(null);
  const mountedRef = useRef(true);
  useEffect(() => () => { mountedRef.current = false; }, []);

  useEffect(() => {
    let cancelled = false;
    api.getProfile()
      .then((p) => { if (!cancelled) setClubProfile(p); })
      .catch(() => { if (!cancelled) setClubProfile(null); });
    return () => { cancelled = true; };
  }, [api]);

  // Язык и имя человек называет про себя один раз на всю сеть — они живут у личности, а не у
  // карточки в клубе, иначе в соседнем клубе он оказался бы другим человеком.
  async function changeLocale(locale: 'ru' | 'en') {
    if (!person) return;
    try {
      await api.updateMyProfile({ displayName: person.displayName, preferredLocale: locale });
      onLocaleChange(locale);
      onPersonChanged();
      toast({ title: t('customer.profile.saved'), variant: 'success' });
    } catch {
      toast({ title: t('customer.profile.saveError'), variant: 'error' });
    }
  }

  async function changeMarketing(marketingOptIn: boolean) {
    try {
      const updated = await api.updateProfile({ marketingOptIn });
      if (mountedRef.current) setClubProfile(updated);
      toast({ title: t('customer.profile.saved'), variant: 'success' });
    } catch {
      toast({ title: t('customer.profile.saveError'), variant: 'error' });
    }
  }

  if (person === null) {
    return <div role="status" aria-label={t('a11y.loading.profile')} className="m-6 h-40 animate-pulse rounded-2xl bg-[var(--color-surface)]" />;
  }

  return (
    <main className="space-y-5 px-6 py-6">
      <header>
        <h1 className="text-2xl font-extrabold tracking-tight">{person.displayName}</h1>
        <p className="mt-1 text-sm text-[var(--text-2)]">
          {person.phoneNumber} ·{' '}
          {/* Подтверждённость решает, доступны ли пополнение и брони, — одна подпись на оба
              состояния лгала бы в одном из них. */}
          <span className="text-[var(--text-3)]">
            {t(person.phoneVerified ? 'customer.profile.phoneNote' : 'customer.profile.phoneUnverified')}
          </span>
        </p>
      </header>

      <PinPanel api={api} pinSet={person.pinSet} onPinSet={onPersonChanged} />

      <section className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4">
        <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">{t('customer.profile.language')}</p>
        <div className="flex gap-2">
          {(['ru', 'en'] as const).map((locale) => (
            <button
              key={locale}
              type="button"
              onClick={() => changeLocale(locale)}
              aria-pressed={person.preferredLocale === locale}
              className={
                'min-h-[44px] flex-1 rounded-xl text-sm font-medium focus-visible:outline-2 focus-visible:outline-[var(--accent)] ' +
                (person.preferredLocale === locale ? 'bg-[var(--accent)] text-[var(--accent-fg)]' : 'border border-[var(--color-border)] text-[var(--text-2)]')
              }
            >
              {locale === 'ru' ? t('customer.profile.langRu') : t('customer.profile.langEn')}
            </button>
          ))}
        </div>
      </section>

      {/* Рассылка — согласие, данное конкретному клубу, поэтому у человека без счёта здесь её
          и нечего спрашивать. */}
      {clubProfile !== null && (
        <label className="flex items-center justify-between rounded-2xl bg-[var(--color-surface)] p-4 text-sm">
          <span>{t('customer.profile.marketing')}</span>
          <input
            type="checkbox"
            checked={clubProfile.marketingOptIn}
            onChange={(event) => changeMarketing(event.target.checked)}
            className="h-5 w-5 accent-[var(--accent)]"
          />
        </label>
      )}

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
