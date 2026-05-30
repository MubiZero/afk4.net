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
    setToasts(prev => [...prev, { id, variant: 'success', ...options }]);
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), autoDismissMs);
  }, [autoDismissMs]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-[60] flex flex-col gap-2" role="region" aria-label="Уведомления">
        {toasts.map(t => (
          <div
            key={t.id}
            role="status"
            className={cn(
              'pointer-events-auto rounded-md border px-4 py-3 text-sm shadow-md',
              t.variant === 'error'
                ? 'border-destructive/30 bg-destructive text-destructive-foreground'
                : 'border-border bg-card text-card-foreground'
            )}
          >
            {t.title}
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
