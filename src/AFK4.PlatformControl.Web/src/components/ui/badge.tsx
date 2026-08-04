import type { ComponentProps } from 'react';

// Бейдж = атом .ui-chip кита в компактном варианте. Состояние передаётся цветом
// (is-live / is-danger / is-warning), а не собственной палитрой.
export type BadgeVariant = 'default' | 'secondary' | 'outline' | 'success' | 'destructive' | 'warning';

const VARIANT_CLASS: Record<BadgeVariant, string> = {
  default: 'ui-chip--status is-booking',
  secondary: 'ui-chip--status is-neutral',
  outline: '',
  success: 'ui-chip--status is-live',
  destructive: 'ui-chip--status is-danger',
  warning: 'ui-chip--status is-warning'
};

export interface BadgeProps extends ComponentProps<'span'> {
  variant?: BadgeVariant;
}

export function Badge({ variant = 'default', className, ...props }: BadgeProps) {
  const classes = ['ui-chip', 'ui-chip--xs', VARIANT_CLASS[variant], className]
    .filter(part => part !== undefined && part !== '')
    .join(' ');
  return <span className={classes} {...props} />;
}
