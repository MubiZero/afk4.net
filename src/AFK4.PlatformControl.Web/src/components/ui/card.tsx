import type { ComponentProps, ReactNode } from 'react';

// Секция экрана = .management-panel из общего кита: приподнятая поверхность с тенью, та же,
// что держит формы разделов «Управления» у оператора. Заголовок секции — .mgmt-section-title
// (мелкая капитель), а не ещё один крупный заголовок рядом с заголовком экрана.
const join = (...parts: (string | undefined)[]) => parts.filter(part => part !== undefined && part !== '').join(' ');

export function Card({ className, ...props }: ComponentProps<'section'>) {
  return <section className={join('management-panel', className)} {...props} />;
}

export function CardHeader({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('mgmt-section-title', className)} {...props} />;
}

export function CardTitle({ className, ...props }: ComponentProps<'span'>) {
  return <span className={className} {...props} />;
}

export function CardDescription({ className, ...props }: ComponentProps<'p'>) {
  return <p className={join('mgmt-drawer-hint', className)} {...props} />;
}

// Строка над содержимым вкладки: пояснение слева, её главная кнопка справа. Заголовка в ней нет
// намеренно — имя вкладки уже стоит в полосе вкладок, и карточка «Кампании» под вкладкой
// «Кампании» повторяла его второй раз (решение владельца 29.09).
export function CardToolbar({ hint, children }: { hint?: ReactNode; children?: ReactNode }) {
  return (
    <div className="pc-card-toolbar">
      {hint !== undefined ? <p className="mgmt-drawer-hint">{hint}</p> : <span />}
      {children}
    </div>
  );
}

export function CardContent({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('pc-panel-body', className)} {...props} />;
}
