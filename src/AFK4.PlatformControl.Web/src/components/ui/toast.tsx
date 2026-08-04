import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { useI18n } from '@/i18n/I18nProvider';

export type ToastVariant = 'success' | 'error';
export interface ToastOptions { title: string; variant?: ToastVariant; }
interface ActiveToast extends ToastOptions { id: number; }

interface ToastContextValue { toast: (options: ToastOptions) => void; }
const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children, autoDismissMs = 4000 }: { children: ReactNode; autoDismissMs?: number }) {
  const { t } = useI18n();
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const nextId = useRef(0);

  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setToasts(prev => [...prev, { id, variant: 'success', ...options }]);
    setTimeout(() => setToasts(prev => prev.filter(item => item.id !== id)), autoDismissMs);
  }, [autoDismissMs]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="toast-viewport" role="region" aria-label={t('toast.region')}>
        {toasts.map(item => (
          <div key={item.id} role="status" className={item.variant === 'error' ? 'toast toast-error' : 'toast toast-success'}>
            <span className="toast-message">{item.title}</span>
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
