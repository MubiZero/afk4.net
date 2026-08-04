import type { ComponentProps } from 'react';

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

export function CardAction({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('pc-cell-actions', className)} {...props} />;
}

export function CardContent({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('pc-panel-body', className)} {...props} />;
}

export function CardFooter({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('mgmt-form-actions', className)} {...props} />;
}
