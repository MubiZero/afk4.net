import type { ComponentProps } from 'react';

// Тумблер — это перерисованный нативный чекбокс (.ui-switch), а не отдельный виджет: форма,
// клавиатура и screen reader работают без обвязки.
export interface SwitchProps extends Omit<ComponentProps<'input'>, 'type' | 'onChange'> {
  onCheckedChange?: (checked: boolean) => void;
}

export function Switch({ className, onCheckedChange, ...props }: SwitchProps) {
  return (
    <input
      type="checkbox"
      role="switch"
      className={['ui-switch', className].filter(Boolean).join(' ')}
      onChange={event => onCheckedChange?.(event.currentTarget.checked)}
      {...props}
    />
  );
}
