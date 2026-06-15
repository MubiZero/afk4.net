import type { ReactNode } from 'react';
import { PanelRightClose, PanelRightOpen } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

// Shell-уровневая обёртка правой зоны: тонкая колонка-ручка с кнопкой сворота + слот контента.
// Сам контент (напр. MapSidePanel) приносит свою `.context-panel` — здесь её НЕ дублируем, иначе
// получилась бы панель-в-панели. Ширину колонки и персист свёрнутости держит App.
export function ContextPanel({ collapsed, onToggle, title, children }: {
  collapsed: boolean;
  onToggle: () => void;
  title?: string;
  children?: ReactNode;
}) {
  const { t } = useI18n();

  if (collapsed) {
    return (
      <aside className="context-zone context-zone-collapsed">
        <button
          type="button"
          className="context-zone-toggle"
          aria-label={t('op.context.expand')}
          onClick={onToggle}
        >
          <PanelRightOpen size={18} />
        </button>
      </aside>
    );
  }

  return (
    <aside className="context-zone">
      <div className="context-zone-handle">
        <button
          type="button"
          className="context-zone-toggle"
          aria-label={t('op.context.collapse')}
          onClick={onToggle}
        >
          <PanelRightClose size={18} />
        </button>
      </div>
      <div className="context-zone-body">
        {title ? <h2 className="context-zone-title">{title}</h2> : null}
        {children}
      </div>
    </aside>
  );
}
