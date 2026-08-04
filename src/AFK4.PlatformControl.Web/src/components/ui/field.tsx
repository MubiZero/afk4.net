import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';

// Поле с подписью = атом .ui-field кита: подпись сверху, контрол под ней, подсказка ниже.
export function Field({ label, htmlFor, hint, children }: {
  label: string;
  htmlFor: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div className="ui-field">
      <label htmlFor={htmlFor}>{label}</label>
      {children}
      {hint !== undefined ? <span className="pc-field-hint">{hint}</span> : null}
    </div>
  );
}

// Ошибка формы — та же полоса с красной кромкой, что у оператора (.workspace-error), а не
// собственный вариант «красная рамка вокруг всего».
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
