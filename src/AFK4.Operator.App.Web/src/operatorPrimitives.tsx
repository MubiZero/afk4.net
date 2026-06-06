import type { ReactNode } from 'react';
import type { CriticalConfirmationTone, Feedback } from './operatorTypes';
import { feedbackText } from './operatorHelpers';

export function FeedbackNotice({ feedback }: { feedback: Feedback }) {
  if (feedback.state === 'idle') {
    return null;
  }

  return (
    <div className={`feedback-notice ${feedback.state}`} role="status" aria-live="polite">
      <span>{feedbackText(feedback)}</span>
    </div>
  );
}

export function CriticalActionConfirmation({
  title,
  detail,
  impact,
  confirmLabel,
  cancelLabel = 'Отмена',
  tone = 'danger',
  disabled = false,
  children,
  onConfirm,
  onCancel
}: {
  title: string;
  detail: string;
  impact: string;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: CriticalConfirmationTone;
  disabled?: boolean;
  children?: ReactNode;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <section className={`critical-confirmation ${tone}`} role="alertdialog" aria-label={title}>
      <div>
        <strong>{title}</strong>
        <span>{detail}</span>
        <em>{impact}</em>
      </div>
      {children}
      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>{cancelLabel}</button>
        <button type="button" className="danger" onClick={onConfirm} disabled={disabled}>{confirmLabel}</button>
      </div>
    </section>
  );
}

export function StateFlag({ label, value, critical }: { label: string; value: string; critical?: boolean }) {
  return (
    <section className={`state-flag${critical ? ' critical' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}
