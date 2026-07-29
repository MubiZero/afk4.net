import type { ReactNode } from 'react';
import { Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface TopbarProps { subtitle: string; screenTitle: string; onOpenSidebar: () => void; search?: ReactNode; right?: ReactNode; }

export function Topbar({ subtitle, screenTitle, onOpenSidebar, search, right }: TopbarProps) {
  return (
    <header className="flex items-center justify-between border-b border-border bg-card px-5 py-3">
      <div className="flex min-w-0 items-center gap-3">
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="menu" onClick={onOpenSidebar}>
          <Menu className="size-4" />
        </Button>
        <div className="text-sm text-muted">
          {subtitle && <>{subtitle} · </>}
          <b className="text-base text-foreground">{screenTitle}</b>
        </div>
      </div>
      <div className="ml-4 flex min-w-0 flex-1 items-center justify-end gap-3">{search}{right}</div>
    </header>
  );
}
