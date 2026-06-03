import { cn } from '@/lib/utils';

export type HistoryView = 'visits' | 'purchases';

export function HistoryTabs({ active, onChange }: { active: HistoryView; onChange: (view: HistoryView) => void }) {
  const tabs: { view: HistoryView; label: string }[] = [
    { view: 'visits', label: 'Визиты' },
    { view: 'purchases', label: 'Покупки' }
  ];
  return (
    <div role="tablist" aria-label="История" className="flex gap-1 px-6 pt-6">
      {tabs.map(({ view, label }) => (
        <button
          key={view}
          type="button"
          role="tab"
          aria-selected={active === view}
          onClick={() => onChange(view)}
          className={cn(
            'min-h-[44px] flex-1 rounded-xl text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-[var(--accent)]',
            active === view ? 'bg-[var(--color-surface-2)] text-[var(--text-1)]' : 'text-[var(--text-3)] hover:text-[var(--text-2)]'
          )}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
