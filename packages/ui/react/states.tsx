import type { ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from './Button';
import { cx } from './cx';

// Что человеку делать перед пустым списком. Проп обязателен, и в этом весь смысл: «Нет товаров»
// без следующего шага оставляет перед стеной, а пока проп был необязательным, его не передал ни
// один список. Молчание тоже бывает верным ответом — но это решение с названной причиной.
export type EmptyNext =
  /** Следующий шаг — кнопка здесь же: завести первое, пригласить, сбросить фильтр. */
  | { kind: 'action'; label: string; onClick: () => void }
  /** Шаг есть, но не у этого человека: кнопки нет, строка называет, у кого право. */
  | { kind: 'denied'; hint: string }
  /** Шаг делается в другом месте; строка называет где. */
  | { kind: 'elsewhere'; hint: string }
  /** Пусто — и это нормально: делать нечего, строка говорит, что здесь появится. */
  | { kind: 'calm'; hint: string }
  /** Поле, форма или главная кнопка раздела, которые решают дело, стоят прямо над списком (в шапке
   *  экрана) — кнопка в пустом состоянии их только повторила бы. */
  | { kind: 'formAbove' };

function nextHint(next: EmptyNext): string | undefined {
  return next.kind === 'denied' || next.kind === 'elsewhere' || next.kind === 'calm' ? next.hint : undefined;
}

// Пустой набор — это реальность, а не ошибка. `inline` — одна строка в классе вызывающего, для
// панелей, где под пустоту места на одну фразу; решение о следующем шаге то же самое.
export function EmptyState({ icon, title, description, next, inline = false, className }: {
  icon?: ReactNode;
  title?: string;
  description?: ReactNode;
  next: EmptyNext;
  inline?: boolean;
  className?: string;
}) {
  const hint = nextHint(next);
  const action = next.kind === 'action' ? next : undefined;
  if (inline) {
    return (
      <p className={className}>
        {[title, description, hint].filter(Boolean).map((line, index) => (
          <span key={index}>{index > 0 ? ' ' : null}{line}</span>
        ))}
        {action ? (
          <>
            {' '}
            <Button variant="ghost" size="sm" onClick={action.onClick}>{action.label}</Button>
          </>
        ) : null}
      </p>
    );
  }
  return (
    <StatePanel icon={icon} title={title} description={description} hint={hint} className={className}>
      {action ? <Button variant="primary" size="sm" className="empty-state-action" onClick={action.onClick}>{action.label}</Button> : null}
    </StatePanel>
  );
}

// Не загрузилось: что именно (`title`), почему (`detail`) и что делать (`hint` — к кому идти за
// доступом). Кнопки повтора нет, когда повтор не поможет: отказ по правам нажатием не чинится, и
// кнопка, обещающая обратное, уводит от единственного настоящего действия.
//
// `inline` — часть экрана не пришла, а остальное уже на виду: одна строка рядом с показанным и
// «Повторить» только для этой части. Деньги и статус остаются на экране, а не тонут под заглушкой.
export function LoadFailure({ title, detail, hint, retry, inline = false, className }: {
  title?: string;
  detail: ReactNode;
  hint?: string;
  retry?: { label: string; onClick: () => void };
  inline?: boolean;
  className?: string;
}) {
  if (inline) {
    return (
      <p className={cx('ui-alert', 'ui-alert--spaced', className)} role="alert">
        {detail}
        {hint ? ` ${hint}` : null}
        {retry ? (
          <>
            {' '}
            <Button variant="ghost" size="sm" onClick={retry.onClick}>{retry.label}</Button>
          </>
        ) : null}
      </p>
    );
  }
  return (
    <StatePanel role="alert" icon={<AlertTriangle size={22} />} title={title} description={detail} hint={hint} className={className}>
      {retry ? <Button variant="primary" size="sm" className="empty-state-action" onClick={retry.onClick}>{retry.label}</Button> : null}
    </StatePanel>
  );
}

function StatePanel({ icon, title, description, hint, role, className, children }: {
  icon?: ReactNode;
  title?: string;
  description?: ReactNode;
  hint?: string;
  role?: 'alert';
  className?: string;
  children?: ReactNode;
}) {
  return (
    <div className={cx('empty-state', className)} role={role}>
      {icon ? <div className="empty-state-icon" aria-hidden="true">{icon}</div> : null}
      {title ? <strong role="heading" aria-level={2}>{title}</strong> : null}
      {description ? <span>{description}</span> : null}
      {hint ? <span>{hint}</span> : null}
      {children}
    </div>
  );
}
