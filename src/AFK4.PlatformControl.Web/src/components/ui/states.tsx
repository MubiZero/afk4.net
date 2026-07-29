import { Button } from './button';
import { Card, CardContent } from './card';
import { Skeleton } from './skeleton';

export function LoadingCards({ count = 4 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
      {Array.from({ length: count }, (_, i) => (
        <Skeleton key={i} data-testid="loading-skeleton" className="h-24 w-full rounded-lg" />
      ))}
    </div>
  );
}

export function ErrorState({ message, retryLabel, onRetry }: { message: string; retryLabel: string; onRetry: () => void }) {
  return (
    <Card><CardContent className="flex flex-col items-center gap-3 py-10">
      <p className="text-muted-foreground">{message}</p>
      <Button onClick={onRetry}>{retryLabel}</Button>
    </CardContent></Card>
  );
}

export function EmptyState({ title, message, action }: { title?: string; message: string; action?: ReactNode }) {
  return (
    <Card><CardContent className="flex flex-col items-center gap-3 py-10 text-center">
      {title !== undefined ? <h2 className="text-base font-semibold text-foreground">{title}</h2> : null}
      <p className="max-w-[60ch] text-sm text-muted-foreground">{message}</p>
      {action}
    </CardContent></Card>
  );
}

export function ForbiddenState({ title, message, actionLabel, onAction }: {
  title: string; message: string; actionLabel: string; onAction: () => void;
}) {
  return (
    <Card role="alert"><CardContent className="flex flex-col items-start gap-3 py-10">
      <h1 className="text-lg font-semibold">{title}</h1>
      <p className="max-w-[60ch] text-sm text-muted-foreground">{message}</p>
      <Button onClick={onAction}>{actionLabel}</Button>
    </CardContent></Card>
  );
}

export function PartialFailure({ title, retryLabel, onRetry }: {
  title: string; retryLabel: string; onRetry: () => void;
}) {
  return (
    <div role="status" className="flex items-center justify-between gap-3 rounded-md border border-warning/50 bg-warning/10 px-4 py-3 text-sm">
      <span>{title}</span>
      <Button variant="outline" size="sm" onClick={onRetry}>{retryLabel}</Button>
    </div>
  );
}
import type { ReactNode } from 'react';
