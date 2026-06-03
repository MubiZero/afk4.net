import { Home, Clock, CalendarDays, User } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { cn } from '@/lib/utils';
import type { PlayerTab } from '@/routing';

const TABS: { tab: PlayerTab; key: MessageKey; Icon: typeof Home }[] = [
  { tab: 'dashboard', key: 'customer.nav.dashboard', Icon: Home },
  { tab: 'history', key: 'customer.nav.history', Icon: Clock },
  { tab: 'reservations', key: 'customer.nav.reservations', Icon: CalendarDays },
  { tab: 'profile', key: 'customer.nav.profile', Icon: User }
];

export function BottomNav({ active, onNavigate }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void }) {
  const { t } = useI18n();
  return (
    <nav className="sticky bottom-0 grid grid-cols-4 border-t border-[var(--color-border)] bg-[var(--color-surface)]"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}>
      {TABS.map(({ tab, key, Icon }) => (
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
