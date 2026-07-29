import type { ReactNode } from 'react';

export function DescriptionList({ items }: { items: { label: string; value: ReactNode }[] }) {
  return <dl className="grid grid-cols-[minmax(8rem,auto)_1fr] gap-x-6 gap-y-2 text-sm">
    {items.flatMap(item => [
      <dt key={`${item.label}-label`} className="text-muted-foreground">{item.label}</dt>,
      <dd key={`${item.label}-value`} className="min-w-0 text-right font-medium">{item.value}</dd>
    ])}
  </dl>;
}
