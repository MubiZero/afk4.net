import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

// Поле формы — та же разметка, что в CRUD-формах Organization Admin: подпись сверху, контрол
// под ней, подсказка ниже. Внешний вид даёт .mgmt-form из общего кита, поэтому поля панели и
// оператора совпадают по высоте, отступам и фокус-кольцу.
export function Field({ label, htmlFor, hint, children }: {
  label: string;
  htmlFor: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label htmlFor={htmlFor}>
      {label}
      {children}
      {hint !== undefined ? <span className="mgmt-drawer-hint">{hint}</span> : null}
    </label>
  );
}

// Ошибка формы — полоса с красной кромкой (.workspace-error), общая для обеих админок.
export function ErrorBanner({ message, dismissLabel, onDismiss }: {
  message: string | null;
  dismissLabel: string;
  onDismiss?: () => void;
}) {
  if (message === null || message.length === 0) return null;
  return (
    <div className="workspace-error" role="alert">
      <AlertTriangle size={15} aria-hidden="true" />
      <span>{message}</span>
      {onDismiss !== undefined ? (
        <button type="button" className="pc-banner-dismiss" onClick={onDismiss}>{dismissLabel}</button>
      ) : null}
    </div>
  );
}
