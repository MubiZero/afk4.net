import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { CheckCircle2, Info, X, XCircle, type LucideIcon } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

export type ToastTone = 'success' | 'error' | 'info';
export interface ToastAction { label: string; onClick: () => void }
export interface ToastOptions { tone: ToastTone; message: string; durationMs?: number; action?: ToastAction }

interface ActiveToast { id: string; tone: ToastTone; message: string; durationMs: number; action?: ToastAction }

export interface ToastApi {
  show: (options: ToastOptions) => string;
  success: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  error: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  info: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  dismiss: (id: string) => void;
}

const MAX_VISIBLE = 3;
const DEFAULT_DURATION = 4000;
const TONE_ICON: Record<ToastTone, LucideIcon> = { success: CheckCircle2, error: XCircle, info: Info };

const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const api = useContext(ToastContext);
  if (api === null) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return api;
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const seq = useRef(0);
  const timers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  const dismiss = useCallback((id: string) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
    const timer = timers.current.get(id);
    if (timer !== undefined) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }, []);

  const show = useCallback((options: ToastOptions) => {
    seq.current += 1;
    const id = String(seq.current);
    setToasts((current) => [...current, {
      id,
      tone: options.tone,
      message: options.message,
      durationMs: options.durationMs ?? DEFAULT_DURATION,
      action: options.action
    }]);
    return id;
  }, []);

  const api = useMemo<ToastApi>(() => ({
    show,
    success: (message, options) => show({ ...options, tone: 'success', message }),
    error: (message, options) => show({ ...options, tone: 'error', message }),
    info: (message, options) => show({ ...options, tone: 'info', message }),
    dismiss
  }), [show, dismiss]);

  const visible = useMemo(() => toasts.slice(0, MAX_VISIBLE), [toasts]);

  // Start the auto-dismiss timer when a toast first becomes VISIBLE (so a queued toast that only
  // appears after a slot frees up still gets its full on-screen lifetime). Errors are sticky.
  useEffect(() => {
    visible.forEach((toast) => {
      if (toast.tone === 'error' || timers.current.has(toast.id)) {
        return;
      }
      timers.current.set(toast.id, setTimeout(() => dismiss(toast.id), toast.durationMs));
    });
  }, [visible, dismiss]);

  useEffect(() => () => {
    timers.current.forEach((timer) => {
      clearTimeout(timer);
    });
    timers.current.clear();
  }, []);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-viewport" role="region" aria-label={t('op.toast.region')}>
        {visible.map((toast) => (
          <ToastCard key={toast.id} toast={toast} closeLabel={t('op.toast.close')} onDismiss={() => dismiss(toast.id)} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastCard({ toast, closeLabel, onDismiss }: { toast: ActiveToast; closeLabel: string; onDismiss: () => void }) {
  const Icon = TONE_ICON[toast.tone];
  const isError = toast.tone === 'error';
  return (
    <div className={`toast toast-${toast.tone}`} role={isError ? 'alert' : 'status'} aria-live={isError ? 'assertive' : 'polite'}>
      <Icon className="toast-icon" aria-hidden="true" size={18} />
      <span className="toast-message">{toast.message}</span>
      {toast.action ? (
        <button type="button" className="toast-action" onClick={() => { toast.action!.onClick(); onDismiss(); }}>
          {toast.action.label}
        </button>
      ) : null}
      <button type="button" className="toast-close" aria-label={closeLabel} onClick={onDismiss}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
