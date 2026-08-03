import type { ReactNode } from 'react';

export function ErrorBanner({ message, dismissLabel, onDismiss }: { message: string | null; dismissLabel: string; onDismiss?: () => void }) {
  if (message === null || message.length === 0) {
    return null;
  }
  return (
    <div className="flex items-center justify-between gap-3 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive" role="alert">
      <span>{message}</span>
      {onDismiss !== undefined && (
        <button type="button" className="min-h-6 underline underline-offset-2" onClick={onDismiss}>
          {dismissLabel}
        </button>
      )}
    </div>
  );
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
