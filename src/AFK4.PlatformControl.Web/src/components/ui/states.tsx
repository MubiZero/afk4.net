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

export function EmptyState({ message }: { message: string }) {
  return (
    <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{message}</CardContent></Card>
  );
}
