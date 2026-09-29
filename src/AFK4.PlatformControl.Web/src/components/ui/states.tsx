import { Inbox } from 'lucide-react';
import { EmptyState as KitEmptyState, LoadFailure } from '@afk4/ui/react';

// Состояния экрана — кит (@afk4/ui/react), тот же, что у Панели. Здесь только словарь панели:
// тридцать с лишним экранов говорят `message`/`next` в её прежних словах, и переучивать их в этой
// правке незачем — вид и поведение уже общие.

/// `title` — что именно не загрузилось, `message` — почему. Кнопки повтора нет, когда повтор не
/// поможет: нехватка прав не чинится нажатием. `onRetry` не передан — блок просто называет причину.
export function ErrorState({ title, message, retryLabel, onRetry }: {
  title?: string; message: string; retryLabel?: string; onRetry?: () => void;
}) {
  return <LoadFailure title={title} detail={message} retry={onRetry !== undefined && retryLabel !== undefined ? { label: retryLabel, onClick: onRetry } : undefined} />;
}

/// Что пустой список предлагает человеку дальше. Проп обязателен: пока он был необязательным,
/// его не передал ни один список панели.
export type EmptyNext =
  /// Кнопка следующего шага: завести первое или сбросить фильтр.
  | { label: string; onClick: () => void }
  /// Завести можно, но не этому сотруднику. Строка говорит, у кого есть право.
  | { noPermission: string }
  /// Пусто — и это хорошо, или список пополняется сам.
  | 'calm'
  /// Здесь это не заводится. Текст называет место, где заводится.
  | 'elsewhere'
  /// Форма или поле, которые решают дело, стоят прямо над списком.
  | 'formAbove';

// У «calm» и «elsewhere» панели сам `message` и есть та строка, которой кит требует от молчания:
// что появится здесь или где это заводится. Поэтому он уходит в `hint`, а не дублируется.
export function EmptyState({ title, message, next }: { title?: string; message: string; next: EmptyNext }) {
  const icon = <Inbox size={22} />;
  if (next === 'calm' || next === 'elsewhere') return <KitEmptyState icon={icon} title={title} next={{ kind: next, hint: message }} />;
  return (
    <KitEmptyState
      icon={icon}
      title={title}
      description={message}
      next={next === 'formAbove' ? { kind: 'formAbove' }
        : 'noPermission' in next ? { kind: 'denied', hint: next.noPermission }
        : { kind: 'action', ...next }}
    />
  );
}

export function ForbiddenState({ title, message, actionLabel, onAction }: {
  title: string; message: string; actionLabel: string; onAction: () => void;
}) {
  return <LoadFailure title={title} detail={message} retry={{ label: actionLabel, onClick: onAction }} />;
}

// Частичный отказ: один блок экрана не загрузился, остальные живы. Строка вместо подмены всего
// экрана — деньги и статус клиента обязаны остаться на виду.
export function PartialFailure({ title, retryLabel, onRetry }: {
  title: string; retryLabel: string; onRetry: () => void;
}) {
  return <LoadFailure inline detail={title} retry={{ label: retryLabel, onClick: onRetry }} />;
}
