import type { ReactNode } from 'react';

interface StatusScreenProps {
  tone?: 'neutral' | 'warning' | 'danger';
  icon: ReactNode;
  title: string;
  body?: ReactNode;
  children?: ReactNode;
  top?: ReactNode;
}

/**
 * Экран, который объясняет, почему сейчас нельзя: нет связи, обслуживание, сбой. Одна мысль,
 * крупно, без кнопок, которые ничего не сделают (спека, §3: «без связи — говорит, чего нет»).
 */
export function StatusScreen({ tone = 'neutral', icon, title, body, children, top }: StatusScreenProps) {
  return (
    <main className={`status-screen status-screen--${tone}`}>
      {top ? <div className="status-screen__top">{top}</div> : null}
      <div className="status-screen__body">
        <span className="status-screen__icon" aria-hidden="true">{icon}</span>
        <h1 className="status-screen__title">{title}</h1>
        {body ? <p className="status-screen__text">{body}</p> : null}
        {children}
      </div>
    </main>
  );
}
