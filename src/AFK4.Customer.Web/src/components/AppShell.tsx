import type { ReactNode } from 'react';
import { BottomNav } from './BottomNav';
import type { PlayerTab } from '@/routing';

export function AppShell({ active, onNavigate, features, children }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void; features: string[] | null; children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col">
      <div className="flex-1 overflow-y-auto pb-2">{children}</div>
      <BottomNav active={active} onNavigate={onNavigate} features={features} />
    </div>
  );
}
