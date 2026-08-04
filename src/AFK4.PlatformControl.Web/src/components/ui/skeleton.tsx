import type { ComponentProps } from 'react';

export function Skeleton({ className, ...props }: ComponentProps<'div'>) {
  return <div className={['skeleton-block', className].filter(Boolean).join(' ')} {...props} />;
}
