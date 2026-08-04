import type { ComponentProps } from 'react';

// Списки панели (счета, тарифы, журнал) — настоящие таблицы, поэтому остаются <table>, но
// выглядят языком кита: приподнятая панель, тихая шапка капителью, строка высотой 56px.
const join = (...parts: (string | undefined)[]) => parts.filter(part => part !== undefined && part !== '').join(' ');

export function Table({ className, ...props }: ComponentProps<'table'>) {
  return (
    <div className="table-panel">
      <table className={join('pc-table', className)} {...props} />
    </div>
  );
}

export function TableHeader(props: ComponentProps<'thead'>) {
  return <thead {...props} />;
}

export function TableBody(props: ComponentProps<'tbody'>) {
  return <tbody {...props} />;
}

export function TableRow(props: ComponentProps<'tr'>) {
  return <tr {...props} />;
}

export function TableHead({ className, ...props }: ComponentProps<'th'>) {
  return <th scope="col" className={className} {...props} />;
}

export function TableCell({ className, ...props }: ComponentProps<'td'>) {
  return <td className={className} {...props} />;
}
