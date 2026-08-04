import type { ReactNode } from 'react';
import { ChevronLeft } from 'lucide-react';

// Каркас экрана панели — тот же, что в «Управлении» Organization Admin: подзаголовок мелкой
// строкой НАД заголовком, затем колонка контента с ограниченной мерой ширины.
//
// Мера ширины не косметика: форма во всю ширину монитора читается как недоделанный экран,
// а таблицу, наоборот, нельзя зажимать — отсюда три варианта колонки.
export function Page({ width = 'wide', back, title, description, actions, children }: {
  width?: 'form' | 'wide' | 'full';
  back?: { label: string; onBack: () => void };
  title?: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="pc-screen">
      {back !== undefined ? (
        <button type="button" className="pc-back" onClick={back.onBack}>
          <ChevronLeft size={14} aria-hidden="true" />
          {back.label}
        </button>
      ) : null}

      {title !== undefined ? (
        <div className="management-screen-head pc-screen-head">
          <div>
            {description !== undefined ? <span>{description}</span> : null}
            <h1>{title}</h1>
          </div>
          {actions !== undefined ? <div className="pc-screen-actions">{actions}</div> : null}
        </div>
      ) : null}

      <div className={`management-content management-content--${width}`}>{children}</div>
    </section>
  );
}
