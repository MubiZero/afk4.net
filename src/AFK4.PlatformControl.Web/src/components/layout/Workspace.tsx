import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

export function Workspace({ children, width = 'wide' }: { children: ReactNode; width?: 'wide' | 'narrow' }) {
  return <div className={cn('mx-auto flex w-full flex-col gap-5', width === 'narrow' ? 'max-w-2xl' : 'max-w-[1600px]')}>{children}</div>;
}
