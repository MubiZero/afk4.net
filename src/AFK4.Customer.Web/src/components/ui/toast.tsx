import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type ToastVariant = 'success' | 'error';
export interface ToastOptions { title: string; variant?: ToastVariant; }
interface ActiveToast extends ToastOptions { id: number; }

interface ToastContextValue { toast: (options: ToastOptions) => void; }
const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children, autoDismissMs = 4000 }: { children: ReactNode; autoDismissMs?: number }) {
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const nextId = useRef(0);

  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setToasts((prev) => [...prev, { variant: 'success', ...options, id }]);
    setTimeout(() => setToasts((prev) => prev.filter((entry) => entry.id !== id)), autoDismissMs);
  }, [autoDismissMs]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4"
        role="region"
        aria-label="Уведомления"
      >
        {toasts.map((entry) => (
          <div
            key={entry.id}
            role="status"
            className={cn(
              'pointer-events-auto w-full max-w-sm rounded-xl border px-4 py-3 text-sm shadow-lg',
              entry.variant === 'error'
                ? 'border-red-500/40 bg-red-500/15 text-red-200'
                : 'border-[var(--color-border)] bg-[var(--color-surface-2)] text-[var(--text-1)]'
            )}
          >
            {entry.title}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (ctx === null) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
