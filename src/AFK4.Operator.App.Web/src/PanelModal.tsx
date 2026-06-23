import { useEffect, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

// Центрированное модальное окно поверх всего приложения (через портал в body) — формы вроде
// старта сессии или расчёта слишком тесны в узкой боковой панели, им нужна полноценная ширина.
export function PanelModal({
  title,
  subtitle,
  onClose,
  children,
  tone
}: {
  title: string;
  subtitle?: string;
  onClose: () => void;
  children: ReactNode;
  tone?: 'warning' | 'danger';
}) {
  const { t } = useI18n();

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  return createPortal(
    <div
      className="panel-modal-backdrop"
      onPointerDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className={`panel-modal${tone ? ` ${tone}` : ''}`} role="dialog" aria-modal="true" aria-label={title}>
        <header className="panel-modal-head">
          <div className="panel-modal-title">
            <strong>{title}</strong>
            {subtitle && <span>{subtitle}</span>}
          </div>
          <button type="button" className="panel-modal-close" aria-label={t('common.cancel')} onClick={onClose}>
            <X size={16} aria-hidden="true" />
          </button>
        </header>
        <div className="panel-modal-body">{children}</div>
      </div>
    </div>,
    document.body
  );
}
