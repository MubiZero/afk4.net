import { Home, Clock, CalendarDays, User } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { cn } from '@/lib/utils';
import type { PlayerTab } from '@/routing';
import type { PlayerFeatureKey } from '@/api/types';

const TABS: { tab: PlayerTab; key: MessageKey; Icon: typeof Home; featureKey?: PlayerFeatureKey }[] = [
  { tab: 'dashboard', key: 'customer.nav.dashboard', Icon: Home },
  { tab: 'history', key: 'customer.nav.history', Icon: Clock },
  { tab: 'reservations', key: 'customer.nav.reservations', Icon: CalendarDays, featureKey: 'online_booking' },
  { tab: 'profile', key: 'customer.nav.profile', Icon: User }
];

export function BottomNav({ active, onNavigate, features }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void; features: string[] | null }) {
  const { t } = useI18n();
  // features === null means "not loaded yet / failed to load" — treat every feature as enabled,
  // see App.tsx for the reasoning (this is UI convenience, not a security gate).
  const visibleTabs = TABS.filter(({ featureKey }) => !featureKey || features === null || features.includes(featureKey));
  return (
    <nav className="sticky bottom-0 grid border-t border-[var(--color-border)] bg-[var(--color-surface)]"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)', gridTemplateColumns: `repeat(${visibleTabs.length}, minmax(0, 1fr))` }}>
      {visibleTabs.map(({ tab, key, Icon }) => (
        <button key={tab} type="button" onClick={() => onNavigate(tab)}
          aria-current={active === tab ? 'page' : undefined}
          className={cn(
            'flex min-h-[56px] flex-col items-center justify-center gap-1 text-xs transition-colors',
            'focus-visible:outline-2 focus-visible:outline-[var(--accent)]',
            active === tab ? 'text-[var(--accent)]' : 'text-[var(--text-3)] hover:text-[var(--text-2)]'
          )}>
          <Icon size={20} aria-hidden />
          {t(key)}
        </button>
      ))}
    </nav>
  );
}
