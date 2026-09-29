import type { ComponentProps } from 'react';
import { StatusBadge, type StatusTone } from '@afk4/ui/react';

// Бейдж панели = статус кита (@afk4/ui/react) с его словарём из пяти тонов. Здесь только прежние
// имена вариантов, на которых говорят экраны панели.
export type BadgeVariant = 'default' | 'secondary' | 'outline' | 'success' | 'destructive' | 'warning';

const TONE: Record<BadgeVariant, StatusTone> = {
  default: 'accent',
  secondary: 'neutral',
  outline: 'neutral',
  success: 'success',
  destructive: 'danger',
  warning: 'warning'
};

export interface BadgeProps extends ComponentProps<'span'> {
  variant?: BadgeVariant;
}

export function Badge({ variant = 'default', ...props }: BadgeProps) {
  return <StatusBadge tone={TONE[variant]} {...props} />;
}
