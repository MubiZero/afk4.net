import type { ComponentProps } from 'react';

// Панель-секция экрана: приподнятая поверхность с шапкой и телом. Глубина здесь — ПОДЪЁМ
// (светлее фона + тень), а не обводка: обводка на каждом блоке и давала тот самый вид
// «всё в белых ободках».
const join = (...parts: (string | undefined)[]) => parts.filter(part => part !== undefined && part !== '').join(' ');

export function Card({ className, ...props }: ComponentProps<'section'>) {
  return <section className={join('pc-card', className)} {...props} />;
}

export function CardHeader({ className, ...props }: ComponentProps<'header'>) {
  return <header className={join('pc-card-head', className)} {...props} />;
}

export function CardTitle({ className, ...props }: ComponentProps<'h2'>) {
  return <h2 className={join('pc-card-title', className)} {...props} />;
}

export function CardDescription({ className, ...props }: ComponentProps<'p'>) {
  return <p className={join('pc-card-desc', className)} {...props} />;
}

export function CardAction({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('pc-card-action', className)} {...props} />;
}

export function CardContent({ className, ...props }: ComponentProps<'div'>) {
  return <div className={join('pc-card-body', className)} {...props} />;
}

export function CardFooter({ className, ...props }: ComponentProps<'footer'>) {
  return <footer className={join('pc-card-foot', className)} {...props} />;
}
