import type { ComponentProps } from 'react';

export interface CheckboxProps extends Omit<ComponentProps<'input'>, 'type' | 'onChange'> {
  onCheckedChange?: (checked: boolean) => void;
}

export function Checkbox({ className, onCheckedChange, ...props }: CheckboxProps) {
  return (
    <input
      type="checkbox"
      className={['pc-checkbox', className].filter(Boolean).join(' ')}
      onChange={event => onCheckedChange?.(event.currentTarget.checked)}
      {...props}
    />
  );
}
