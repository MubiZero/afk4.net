import type { ReactNode } from 'react';
import { ChevronLeft } from 'lucide-react';

// Каркас экрана: опциональный возврат, заголовок с действиями, тело.
export function Page({ width, back, title, description, actions, children }: {
  width?: 'narrow';
  back?: { label: string; onBack: () => void };
  title?: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className={width === 'narrow' ? 'pc-page pc-page--narrow' : 'pc-page'}>
      {back !== undefined ? (
        <button type="button" className="pc-back" onClick={back.onBack}>
          <ChevronLeft size={14} aria-hidden="true" />
          {back.label}
        </button>
      ) : null}
      {title !== undefined ? (
        <div className="pc-page-head">
          <div>
            <h1>{title}</h1>
            {description !== undefined ? <p>{description}</p> : null}
          </div>
          {actions !== undefined ? <div className="pc-page-actions">{actions}</div> : null}
        </div>
      ) : null}
      {children}
    </div>
  );
}
