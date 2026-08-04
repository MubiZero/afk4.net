import type { ComponentProps } from 'react';

export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return <textarea className={['ui-textarea', className].filter(Boolean).join(' ')} {...props} />;
}
