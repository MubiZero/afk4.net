import { Button as KitButton, type ButtonProps as KitButtonProps } from '@afk4/ui/react';

// Кнопка панели = кнопка кита (@afk4/ui/react), та же, что у Панели. Здесь только прежние имена
// вариантов, на которых говорят экраны панели: `default` — главная, `outline`/`secondary` — обычная,
// `destructive` — опасная. Кнопка под одну иконку — IconButton кита напрямую: у неё обязательная
// подпись, а у этой обёртки её не было бы.
export type ButtonVariant = 'default' | 'outline' | 'secondary' | 'ghost' | 'destructive';
export type ButtonSize = 'default' | 'sm' | 'lg';

const VARIANT: Record<ButtonVariant, KitButtonProps['variant']> = {
  default: 'primary',
  outline: 'secondary',
  secondary: 'secondary',
  ghost: 'ghost',
  destructive: 'danger'
};

const SIZE: Record<ButtonSize, KitButtonProps['size']> = { default: 'md', sm: 'sm', lg: 'lg' };

export interface ButtonProps extends Omit<KitButtonProps, 'variant' | 'size'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export function Button({ variant = 'default', size = 'default', ...props }: ButtonProps) {
  return <KitButton variant={VARIANT[variant]} size={SIZE[size]} {...props} />;
}
