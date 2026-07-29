import type { ReactNode } from 'react';

export function Loading({ label }: { label?: string }) {
  return (
    <div className="text-sm text-muted-foreground" role="status" aria-live="polite">
      {label ?? 'Loading…'}
    </div>
  );
}

export function ErrorBanner({ message, onDismiss }: { message: string | null; onDismiss?: () => void }) {
  if (message === null || message.length === 0) {
    return null;
  }
  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive" role="alert">
      <span>{message}</span>
      {onDismiss !== undefined && (
        <button type="button" className="min-h-6 underline underline-offset-2" onClick={onDismiss}>
          Dismiss
        </button>
      )}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="rounded-md border border-dashed border-border p-4 text-center text-sm text-muted-foreground">{children}</div>;
}

export function Field({
  label,
  htmlFor,
  hint,
  children
}: {
  label: string;
  htmlFor: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label htmlFor={htmlFor} className="flex flex-col gap-1.5">
      <span className="text-sm font-medium">{label}</span>
      {children}
      {hint !== undefined && <span className="text-xs text-muted-foreground">{hint}</span>}
    </label>
  );
}

export function StatusBadge({ status }: { status: string }) {
  return <span className="inline-flex rounded-full border border-border px-2 py-0.5 text-xs font-semibold">{status}</span>;
}

export function formatDate(value: string | null): string {
  if (value === null) {
    return '—';
  }
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) {
    return value;
  }
  return new Date(parsed).toLocaleString();
}
