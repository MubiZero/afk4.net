import { AlertTriangle, Inbox } from 'lucide-react';
import { Button } from './button';

/// `title` — что именно не загрузилось, `message` — почему. Порознь они неполны: «не удалось
/// загрузить фичи клуба» не подсказывает, чинить ли права или подождать сервер, а голое
/// «недостаточно прав» на странице с семью разделами не говорит, к какому из них это относится.
/// Кнопки повтора нет, когда повтор не поможет: нехватка прав не чинится нажатием, и кнопка,
/// которая обещает обратное, уводит человека от единственного настоящего действия — попросить
/// доступ. `onRetry` не передан — блок просто называет причину.
export function ErrorState({ title, message, retryLabel, onRetry }: {
  title?: string; message: string; retryLabel?: string; onRetry?: () => void;
}) {
  return (
    <div className="empty-state" role="alert">
      <AlertTriangle className="empty-state-icon" size={22} aria-hidden="true" />
      {title !== undefined ? <h2>{title}</h2> : null}
      <p>{message}</p>
      {onRetry !== undefined && retryLabel !== undefined ? <Button onClick={onRetry}>{retryLabel}</Button> : null}
    </div>
  );
}

/// Что пустой список предлагает человеку дальше. Проп обязателен, и в этом весь смысл: «Нет
/// приглашений» без следующего шага оставляет человека гадать, что делать, — а пока проп был
/// необязательным, его не передал ни один список панели. Молчание тоже бывает верным ответом,
/// но теперь это решение с названной причиной, а не забытый аргумент.
export type EmptyNext =
  /// Кнопка следующего шага: завести первое или сбросить фильтр, под который ничего не подошло.
  | { label: string; onClick: () => void }
  /// Завести можно, но не этому сотруднику. Строка говорит, у кого есть право, — вместо кнопки,
  /// на которую сервер ответил бы отказом.
  | { noPermission: string }
  /// Пусто — и это хорошо, или список пополняется сам. Текст говорит, что всё в порядке и что
  /// появится здесь, когда случится.
  | 'calm'
  /// Здесь это не заводится. Текст называет место, где заводится.
  | 'elsewhere'
  /// Форма или поле, которые решают дело, стоят прямо над списком; кнопка их только повторила бы.
  | 'formAbove';

export function EmptyState({ title, message, next }: { title?: string; message: string; next: EmptyNext }) {
  return (
    <div className="empty-state">
      <Inbox className="empty-state-icon" size={22} aria-hidden="true" />
      {title !== undefined ? <h2>{title}</h2> : null}
      <p>{message}</p>
      {typeof next === 'string' ? null
        : 'noPermission' in next ? <p className="mgmt-drawer-hint">{next.noPermission}</p>
        : <Button size="sm" className="empty-state-action" onClick={next.onClick}>{next.label}</Button>}
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
