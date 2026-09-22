import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorErrorProjection } from './apiErrors';
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
    <section className={`ui-chip ui-chip--count${critical ? ' is-critical' : ''}${tone ? ` is-${tone}` : ''}`}>
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

// Часть экрана не загрузилась, а остальное уже на виду: что не пришло и почему — одной строкой,
// и «Повторить», который перезапрашивает только эту часть. Панель, которую отказ заменяет целиком,
// рисуется через LoadFailureState; эта строка — для подписи рядом с тем, что показано.
//
// Кнопка есть, только когда повтор может помочь (`failure.retryCanHelp`): под отказом по правам
// она обещала бы то, чего не будет. Вместо неё там названо, к кому идти за доступом.
export function PartialLoadFailure({ text, failure, onRetry }: { text: string; failure: OperatorErrorProjection; onRetry: () => void }) {
  const { t } = useI18n();
  return (
    <p className="ui-alert ui-alert--spaced" role="alert">
      {text}
      {failure.accessHint ? ` ${failure.accessHint}` : null}
      {failure.retryCanHelp ? (
        <>
          {' '}
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm" onClick={onRetry}>
            {t('op.management.state.retry')}
          </button>
        </>
      ) : null}
    </p>
  );
}

// Панель или весь экран, которые отказ загрузки заменяет целиком: что не загрузилось, почему и
// что делать дальше. «Повторить» — по тому же признаку, что и у строки выше.
export function LoadFailureState({ title, failure, onRetry }: { title: string; failure: OperatorErrorProjection; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <EmptyState
      title={title}
      description={failure.detail}
      hint={failure.accessHint}
      action={failure.retryCanHelp && onRetry ? { label: t('op.management.state.retry'), onClick: onRetry } : undefined}
    />
  );
}

export function EmptyState({
  icon,
  title,
  description,
  hint,
  action,
  className
}: {
  icon?: ReactNode;
  title: string;
  description?: string;
  hint?: string;
  action?: { label: string; onClick: () => void };
  className?: string;
}) {
  return (
    <div className={`empty-state${className ? ` ${className}` : ''}`}>
      {icon ? <div className="empty-state-icon" aria-hidden="true">{icon}</div> : null}
      <strong>{title}</strong>
      {description ? <span>{description}</span> : null}
      {hint ? <span>{hint}</span> : null}
      {action ? (
        <button type="button" className="ui-btn ui-btn--primary ui-btn--sm empty-state-action" onClick={action.onClick}>{action.label}</button>
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
