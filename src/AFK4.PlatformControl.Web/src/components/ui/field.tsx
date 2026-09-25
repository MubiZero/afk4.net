import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

// Поле формы — та же разметка, что в CRUD-формах Organization Admin: подпись сверху, контрол
// под ней, подсказка ниже. Внешний вид даёт .mgmt-form из общего кита, поэтому поля панели и
// оператора совпадают по высоте, отступам и фокус-кольцу.
//
// Ошибка стоит у самого поля, а не общей полосой над формой: человек видит, что чинить, там, куда
// смотрит. Её id — `${htmlFor}-error`: контрол ссылается на него через aria-describedby.
export function fieldErrorId(htmlFor: string): string {
  return `${htmlFor}-error`;
}

export function Field({ label, htmlFor, hint, error, children }: {
  label: string;
  htmlFor: string;
  hint?: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <label htmlFor={htmlFor}>
      {label}
      {children}
      {error !== undefined ? <span id={fieldErrorId(htmlFor)} className="pc-error-text">{error}</span> : null}
      {hint !== undefined ? <span className="mgmt-drawer-hint">{hint}</span> : null}
    </label>
  );
}

// Ошибка формы — полоса с красной кромкой (.ui-alert ui-alert--spaced), общая для обеих админок.
export function ErrorBanner({ message, dismissLabel, onDismiss }: {
  message: string | null;
  dismissLabel: string;
  onDismiss?: () => void;
}) {
  if (message === null || message.length === 0) return null;
  return (
    <div className="ui-alert ui-alert--spaced" role="alert">
      <AlertTriangle size={15} aria-hidden="true" />
      <span>{message}</span>
      {onDismiss !== undefined ? (
        <button type="button" className="pc-banner-dismiss" onClick={onDismiss}>{dismissLabel}</button>
      ) : null}
    </div>
  );
}
