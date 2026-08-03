import type { ReactNode } from 'react';

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="text-balance text-2xl font-bold tracking-tight">{title}</h1>
        {description !== undefined ? <p className="mt-1 max-w-[70ch] text-pretty text-sm text-muted-foreground">{description}</p> : null}
      </div>
      {actions !== undefined ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </header>
  );
}
