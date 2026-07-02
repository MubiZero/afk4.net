import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import type { CriticalConfirmationTone, Feedback } from './operatorTypes';
import { feedbackText } from './operatorHelpers';
import { formatMinorUnits } from './currencyFormat';

export function FeedbackNotice({ feedback }: { feedback: Feedback }) {
  const { t } = useI18n();
  if (feedback.state === 'idle') {
    return null;
  }

  return (
    <div className={`feedback-notice ${feedback.state}`} role="status" aria-live="polite">
      <span>{feedbackText(feedback, t)}</span>
    </div>
  );
}

export function CriticalActionConfirmation({
  title,
  detail,
  impact,
  confirmLabel,
  cancelLabel,
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
  const { t } = useI18n();

  return (
    <section className={`critical-confirmation ${tone}`} role="alertdialog" aria-label={title}>
      <div>
        <strong>{title}</strong>
        <span>{detail}</span>
        <em>{impact}</em>
      </div>
      {children}
      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>{cancelLabel ?? t('common.cancel')}</button>
        <button type="button" className="danger" onClick={onConfirm} disabled={disabled}>{confirmLabel}</button>
      </div>
    </section>
  );
}

export function StateFlag({ label, value, critical, tone }: { label: string; value: ReactNode; critical?: boolean; tone?: 'warning' }) {
  return (
    <section className={`state-flag${critical ? ' critical' : ''}${tone ? ` ${tone}` : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}

export function Skeleton({
  variant = 'block',
  lines = 1,
  className
}: {
  variant?: 'block' | 'text' | 'circle';
  lines?: number; // only applies to variant="text"; ignored for block/circle
  className?: string;
}) {
  if (variant === 'text') {
    return (
      <div className={`skeleton-text-group${className ? ` ${className}` : ''}`} aria-hidden="true">
        {Array.from({ length: lines }).map((_, index) => (
          <div key={index} className="skeleton-block skeleton-text" />
        ))}
      </div>
    );
  }
  const shape = variant === 'circle' ? ' skeleton-circle' : '';
  return <div className={`skeleton-block${shape}${className ? ` ${className}` : ''}`} aria-hidden="true" />;
}

export function EmptyState({
  icon,
  title,
  description,
  action,
  className
}: {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: { label: string; onClick: () => void };
  className?: string;
}) {
  return (
    <div className={`empty-state${className ? ` ${className}` : ''}`}>
      {icon ? <div className="empty-state-icon" aria-hidden="true">{icon}</div> : null}
      <strong>{title}</strong>
      {description ? <span>{description}</span> : null}
      {action ? (
        <button type="button" className="empty-state-action" onClick={action.onClick}>{action.label}</button>
      ) : null}
    </div>
  );
}

export function Money({
  minorUnits,
  currencyCode,
  signed = false
}: {
  minorUnits: number;
  currencyCode: string;
  signed?: boolean;
}) {
  if (!signed) {
    return <span className="ui-money">{formatMinorUnits(minorUnits, currencyCode)}</span>;
  }
  const positive = minorUnits >= 0;
  const text = formatMinorUnits(Math.abs(minorUnits), currencyCode);
  return (
    <span className={`ui-money ${positive ? 'ui-money--pos' : 'ui-money--neg'}`}>
      {positive ? '+' : '−'}{text}
    </span>
  );
}
