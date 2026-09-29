import type { ComponentProps, ReactNode } from 'react';
import { X } from 'lucide-react';
import { cx } from './cx';

// Кнопка — атом .ui-btn. Вариантов четыре, и каждый — роль, а не цвет:
//   primary   — одна главная на экран или карточку, следующий шаг;
//   secondary — обычное действие, кант без заливки (по умолчанию: главной надо назначить явно);
//   ghost     — тихое действие в строке текста;
//   danger    — необратимое, красным кантом, а не заливкой.
// До кита одно и то же действие рисовалось то заливкой, то обводкой, а на карте зала — вовсе
// своими кнопками мимо .ui-btn.
export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

const VARIANT: Record<ButtonVariant, string | undefined> = {
  primary: 'ui-btn--primary',
  secondary: undefined,
  ghost: 'ui-btn--ghost',
  danger: 'ui-btn--danger',
};

const SIZE: Record<ButtonSize, string | undefined> = {
  sm: 'ui-btn--sm',
  md: undefined,
  lg: 'ui-btn--lg',
};

export interface ButtonProps extends ComponentProps<'button'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Во всю ширину родителя — главная кнопка инспектора. */
  block?: boolean;
}

export function Button({ variant = 'secondary', size = 'md', block = false, className, type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={cx('ui-btn', VARIANT[variant], SIZE[size], block && 'ui-btn--block', className)} {...props} />;
}

// Кнопка под одну иконку. Подпись обязательна: у кнопки без текста доступное имя — только
// aria-label, и забытый проп оставлял экранному диктору голое «кнопка». Та же подпись уходит во
// всплывающую подсказку — мыши тоже не угадывать, что значит значок.
export interface IconButtonProps extends Omit<ComponentProps<'button'>, 'children' | 'aria-label'> {
  label: string;
  icon: ReactNode;
  variant?: Exclude<ButtonVariant, 'primary'>;
  size?: Exclude<ButtonSize, 'lg'>;
}

export function IconButton({ label, icon, variant = 'secondary', size = 'md', className, type = 'button', title, ...props }: IconButtonProps) {
  return (
    <button
      {...props}
      type={type}
      aria-label={label}
      title={title ?? label}
      className={cx('ui-btn', 'ui-btn--icon', VARIANT[variant], SIZE[size], className)}
    >
      {icon}
    </button>
  );
}

// Закрыть панель, диалог, инспектор. Было девять реализаций ✕ — разных размеров, с кантом и без,
// с подписью и без.
export function CloseButton({ label, ...props }: Omit<IconButtonProps, 'icon' | 'variant'>) {
  return <IconButton label={label} icon={<X size={16} aria-hidden="true" />} {...props} />;
}
