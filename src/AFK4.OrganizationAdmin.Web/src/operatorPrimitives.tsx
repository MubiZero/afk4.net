import { useEffect, useRef, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorErrorProjection } from './apiErrors';
import type { CriticalConfirmationTone, Feedback } from './operatorTypes';
import { feedbackText } from './operatorHelpers';
import { CountChip, LoadFailure } from '@afk4/ui/react';

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
  const sectionRef = useRef<HTMLElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  // Подтверждение стоит в потоке экрана, а не поверх него: в разделах со списком и карточкой оно
  // вставало под списком, за краем видимой области, и человек, нажавший «Отключить» в карточке,
  // не видел, что от него ждут ответа. Появившись, оно показывается и забирает фокус — на
  // «Отмену», а не на опасную кнопку.
  useEffect(() => {
    sectionRef.current?.scrollIntoView?.({ block: 'nearest' });
    cancelRef.current?.focus();
  }, []);

  return (
    <section ref={sectionRef} className={`critical-confirmation ${tone}`} role="alertdialog" aria-label={title}>
      <div>
        <strong>{title}</strong>
        <span>{detail}</span>
        <em>{impact}</em>
      </div>
      {children}
      <div className="critical-confirmation-actions">
        <button ref={cancelRef} type="button" onClick={onCancel} disabled={disabled}>{cancelLabel ?? t('common.cancel')}</button>
        <button type="button" className="danger" onClick={onConfirm} disabled={disabled}>{confirmLabel}</button>
      </div>
    </section>
  );
}

// Счётчик в шапке раздела — CountChip кита. `critical` — прежнее имя тона тревоги.
export function StateFlag({ label, value, critical, tone }: { label: string; value: ReactNode; critical?: boolean; tone?: 'warning' }) {
  return <CountChip label={label} value={value} tone={critical ? 'critical' : tone} />;
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
// и «Повторить», который перезапрашивает только эту часть. Вид — LoadFailure кита; здесь только
// перевод отказа сервера (OperatorErrorProjection) в его слова.
//
// Кнопка есть, только когда повтор может помочь (`failure.retryCanHelp`): под отказом по правам
// она обещала бы то, чего не будет. Вместо неё там названо, к кому идти за доступом.
export function PartialLoadFailure({ text, failure, onRetry }: { text: string; failure: OperatorErrorProjection; onRetry: () => void }) {
  const { t } = useI18n();
  return (
    <LoadFailure
      inline
      detail={text}
      hint={failure.accessHint}
      retry={failure.retryCanHelp ? { label: t('op.management.state.retry'), onClick: onRetry } : undefined}
    />
  );
}

// Панель или весь экран, которые отказ загрузки заменяет целиком: что не загрузилось, почему и
// что делать дальше. «Повторить» — по тому же признаку, что и у строки выше.
export function LoadFailureState({ title, failure, onRetry }: { title: string; failure: OperatorErrorProjection; onRetry?: () => void }) {
  const { t } = useI18n();
  return (
    <LoadFailure
      title={title}
      detail={failure.detail}
      hint={failure.accessHint}
      retry={failure.retryCanHelp && onRetry ? { label: t('op.management.state.retry'), onClick: onRetry } : undefined}
    />
  );
}

// Пустой набор и деньги — прямо из кита: у Панели и Platform Control они одни.
export { EmptyState, Money, type EmptyNext as EmptyStateNext } from '@afk4/ui/react';
