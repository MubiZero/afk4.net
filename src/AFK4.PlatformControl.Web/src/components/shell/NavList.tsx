import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { Badge } from '@/components/ui/badge';
import type { NavGroup } from './navModel';

export interface NavListProps {
  groups: NavGroup[];
  activePath: string;
  counts?: Record<string, number>;
  onNavigate: (path: string) => void;
}

export function NavList({ groups, activePath, counts = {}, onNavigate }: NavListProps) {
  const { t } = useI18n();
  return (
    <nav className="flex flex-col gap-1">
      {groups.map(group => (
        <div key={group.key} className="px-2 py-1">
          <div className="px-3 pb-1 pt-3 text-[10px] font-bold uppercase tracking-wide text-muted">
            {t(group.labelKey)}
          </div>
          {group.items.map(item => {
            const active = item.path === activePath;
            const count = counts[item.key];
            return (
              <button
                key={item.key}
                type="button"
                aria-current={active ? 'page' : undefined}
                onClick={() => onNavigate(item.path)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm font-medium text-foreground/80 hover:bg-accent',
                  active && 'bg-accent font-semibold text-accent-foreground'
                )}
              >
                <span>{t(item.labelKey)}</span>
                {typeof count === 'number' && count > 0 && (
                  <Badge variant="secondary" className="ml-auto">{count}</Badge>
                )}
                {item.soon && !(typeof count === 'number' && count > 0) && (
                  <span className="ml-auto text-[10px] text-muted">{t('shell.soon')}</span>
                )}
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
