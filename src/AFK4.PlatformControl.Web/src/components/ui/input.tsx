import type { ComponentProps } from 'react';

export function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={['ui-input', className].filter(Boolean).join(' ')} {...props} />;
}
