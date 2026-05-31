import type { ReactNode } from 'react';
import { Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface TopbarProps { subtitle: string; screenTitle: string; onOpenSidebar: () => void; right?: ReactNode; }

export function Topbar({ subtitle, screenTitle, onOpenSidebar, right }: TopbarProps) {
  return (
    <header className="flex items-center justify-between border-b border-border bg-card px-5 py-3">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="menu" onClick={onOpenSidebar}>
          <Menu className="size-4" />
        </Button>
        <div className="text-sm text-muted">
          {subtitle && <>{subtitle} · </>}
          <b className="text-base text-foreground">{screenTitle}</b>
        </div>
      </div>
      {right}
    </header>
  );
}
