import type { ReactNode } from 'react';

// Shell-уровневая обёртка правой зоны: просто слот контента. Сам контент (напр. MapSidePanel)
// приносит свою `.context-panel` — здесь её НЕ дублируем. Кнопка сворачивания убрана: панель
// и так появляется только когда выбрано место, сворачивать её незачем.
export function ContextPanel({ title, children }: {
  title?: string;
  children?: ReactNode;
}) {
  return (
    <aside className="context-zone">
      <div className="context-zone-body">
        {title ? <h2 className="context-zone-title">{title}</h2> : null}
        {children}
      </div>
    </aside>
  );
}
