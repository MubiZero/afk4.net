import { useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { NavList } from './NavList';
import { UserMenu } from './UserMenu';
import { Topbar } from './Topbar';
import type { NavGroup } from './navModel';

export interface AppShellProps {
  navGroups: NavGroup[];
  sidebarHeader: ReactNode;
  activePath: string;
  subtitle: string;
  screenTitle: string;
  menuLabel: string;
  userName: string;
  roleLabel: string;
  counts?: Record<string, number>;
  topbarRight?: ReactNode;
  topbarSearch?: ReactNode;
  onNavigate: (path: string) => void;
  onSignOut: () => void;
  children: ReactNode;
}

export function AppShell(props: AppShellProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border bg-card transition-transform md:static md:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        {props.sidebarHeader}
        <div className="flex-1 overflow-auto">
          <NavList groups={props.navGroups} activePath={props.activePath} counts={props.counts}
            onNavigate={(p) => { setSidebarOpen(false); props.onNavigate(p); }} />
        </div>
        <UserMenu displayName={props.userName} roleLabel={props.roleLabel} onSignOut={props.onSignOut} />
      </aside>

      {sidebarOpen && (
        <div className="fixed inset-0 z-30 bg-black/40 md:hidden" onClick={() => setSidebarOpen(false)} aria-hidden />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar subtitle={props.subtitle}
          screenTitle={props.screenTitle} menuLabel={props.menuLabel} onOpenSidebar={() => setSidebarOpen(true)} search={props.topbarSearch} right={props.topbarRight} />
        <main className="flex-1 overflow-auto p-5">{props.children}</main>
      </div>
    </div>
  );
}
