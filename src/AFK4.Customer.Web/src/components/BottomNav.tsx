import { Home, Clock, CalendarDays, User } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { PlayerTab } from '@/routing';

const TABS: { tab: PlayerTab; label: string; Icon: typeof Home }[] = [
  { tab: 'dashboard', label: 'Главная', Icon: Home },
  { tab: 'history', label: 'История', Icon: Clock },
  { tab: 'reservations', label: 'Брони', Icon: CalendarDays },
  { tab: 'profile', label: 'Профиль', Icon: User }
];

export function BottomNav({ active, onNavigate }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void }) {
  return (
    <nav className="sticky bottom-0 grid grid-cols-4 border-t border-[var(--color-border)] bg-[var(--color-surface)]"
      style={{ paddingBottom: 'env(safe-area-inset-bottom)' }}>
      {TABS.map(({ tab, label, Icon }) => (
        <button key={tab} type="button" onClick={() => onNavigate(tab)}
          aria-current={active === tab ? 'page' : undefined}
          className={cn(
            'flex min-h-[56px] flex-col items-center justify-center gap-1 text-xs transition-colors',
            'focus-visible:outline-2 focus-visible:outline-[var(--accent)]',
            active === tab ? 'text-[var(--accent)]' : 'text-[var(--text-3)] hover:text-[var(--text-2)]'
          )}>
          <Icon size={20} aria-hidden />
          {label}
        </button>
      ))}
    </nav>
  );
}
