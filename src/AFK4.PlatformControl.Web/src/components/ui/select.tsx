import type { ComponentProps } from 'react';

// Нативный <select> вместо собранного из примитивов выпадающего списка: тот же контрол, что
// в формах Organization Admin, бесплатная клавиатура и мобильное поведение, минус зависимость.
// Внешний вид задаёт атом .ui-select кита.
export function Select({ className, ...props }: ComponentProps<'select'>) {
  return <select className={['ui-select', className].filter(Boolean).join(' ')} {...props} />;
}
