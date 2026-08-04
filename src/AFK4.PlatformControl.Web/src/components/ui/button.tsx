import type { ComponentProps } from 'react';

// Кнопка панели = атом .ui-btn из @afk4/ui. Здесь только сопоставление пропсов с модификаторами
// кита: своих размеров и цветов панель не заводит — иначе она снова разъедется с Organization
// Admin, как разъехалась на шаблонных примитивах.
export type ButtonVariant = 'default' | 'outline' | 'secondary' | 'ghost' | 'destructive';
export type ButtonSize = 'default' | 'sm' | 'lg' | 'icon' | 'icon-sm';

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  default: 'ui-btn--primary',
  outline: '',
  secondary: '',
  ghost: 'ui-btn--ghost',
  destructive: 'ui-btn--danger'
};

const SIZE_CLASS: Record<ButtonSize, string> = {
  default: '',
  sm: 'ui-btn--sm',
  lg: 'ui-btn--lg',
  icon: 'ui-btn--icon',
  'icon-sm': 'ui-btn--icon ui-btn--sm'
};

export interface ButtonProps extends ComponentProps<'button'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export function Button({ variant = 'default', size = 'default', className, type = 'button', ...props }: ButtonProps) {
  const classes = ['ui-btn', VARIANT_CLASS[variant], SIZE_CLASS[size], className]
    .filter(part => part !== undefined && part !== '')
    .join(' ');
  return <button type={type} className={classes} {...props} />;
}
