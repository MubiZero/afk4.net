import type { ReactNode } from 'react';
import { PanelRightClose, PanelRightOpen } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

export function ContextPanel({ collapsed, onToggle, title, children }: {
  collapsed: boolean;
  onToggle: () => void;
  title?: string;
  children: ReactNode;
}) {
  const { t } = useI18n();

  if (collapsed) {
    return (
      <aside className="context-panel context-panel-collapsed">
        <button
          type="button"
          className="context-panel-toggle"
          aria-label={t('op.context.expand')}
          onClick={onToggle}
        >
          <PanelRightOpen size={18} />
        </button>
      </aside>
    );
  }

  return (
    <aside className="context-panel">
      <header className="context-head">
        {title ? <h2>{title}</h2> : null}
        <button
          type="button"
          className="context-panel-toggle"
          aria-label={t('op.context.collapse')}
          onClick={onToggle}
        >
          <PanelRightClose size={18} />
        </button>
      </header>
      {children}
    </aside>
  );
}
