import type { ReactNode } from 'react';
import { AlertTriangle, Inbox } from 'lucide-react';
import { Button } from './button';
import { Skeleton } from './skeleton';

// Скелетон повторяет финальную геометрию списка (строка 56px в приподнятой панели), чтобы
// содержимое подменялось без прыжка раскладки.
export function LoadingCards({ count = 4 }: { count?: number }) {
  return (
    <div className="table-panel">
      <div className="ctable-skeleton">
        {Array.from({ length: count }, (_, index) => (
          <Skeleton key={index} data-testid="loading-skeleton" className="ctable-row-skel" />
        ))}
      </div>
    </div>
  );
}

/// `title` — что именно не загрузилось, `message` — почему. Порознь они неполны: «не удалось
/// загрузить фичи клуба» не подсказывает, чинить ли права или подождать сервер, а голое
/// «недостаточно прав» на странице с семью разделами не говорит, к какому из них это относится.
export function ErrorState({ title, message, retryLabel, onRetry }: {
  title?: string; message: string; retryLabel: string; onRetry: () => void;
}) {
  return (
    <div className="empty-state" role="alert">
      <AlertTriangle className="empty-state-icon" size={22} aria-hidden="true" />
      {title !== undefined ? <h2>{title}</h2> : null}
      <p>{message}</p>
      <Button onClick={onRetry}>{retryLabel}</Button>
    </div>
  );
}

export function EmptyState({ title, message, action }: { title?: string; message: string; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <Inbox className="empty-state-icon" size={22} aria-hidden="true" />
      {title !== undefined ? <h2>{title}</h2> : null}
      <p>{message}</p>
      {action}
    </div>
  );
}

export function ForbiddenState({ title, message, actionLabel, onAction }: {
  title: string; message: string; actionLabel: string; onAction: () => void;
}) {
  return (
    <div className="empty-state" role="alert">
      <AlertTriangle className="empty-state-icon" size={22} aria-hidden="true" />
      <h2>{title}</h2>
      <p>{message}</p>
      <Button onClick={onAction}>{actionLabel}</Button>
    </div>
  );
}

// Частичный отказ: один блок экрана не загрузился, остальные живы. Полоса предупреждения
// вместо подмены всего экрана — деньги и статус клиента обязаны остаться на виду.
export function PartialFailure({ title, retryLabel, onRetry }: {
  title: string; retryLabel: string; onRetry: () => void;
}) {
  return (
    <div role="status" className="pc-partial">
      <span>{title}</span>
      <Button variant="outline" size="sm" onClick={onRetry}>{retryLabel}</Button>
    </div>
  );
}
