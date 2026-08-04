import { useEffect, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { useI18n } from '@/i18n/I18nProvider';

// Модальное окно панели — та же .panel-modal из @afk4/ui, что и в Organization Admin: портал в
// body, закрытие по Escape и по клику вне окна.
//
// Обёртка написана здесь, а не в @afk4/ui: пакет сознательно оставлен CSS-только (общий ВИД),
// иначе он тянет React в сборку обоих приложений. Если появится третье приложение — обёртки
// пора выносить в @afk4/ui/react, пока цена дублирования ниже цены общей React-зависимости.
export function Dialog({ open, title, description, tone, onClose, children, footer }: {
  open: boolean;
  title: string;
  description?: string;
  tone?: 'warning' | 'danger';
  onClose: () => void;
  children?: ReactNode;
  footer?: ReactNode;
}) {
  const { t } = useI18n();

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div
      className="panel-modal-backdrop"
      onPointerDown={event => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div className={tone === undefined ? 'panel-modal' : `panel-modal ${tone}`} role="dialog" aria-modal="true" aria-label={title}>
        <header className="panel-modal-head">
          <div className="panel-modal-title">
            <strong>{title}</strong>
            {description !== undefined ? <span>{description}</span> : null}
          </div>
          <button type="button" className="panel-modal-close" aria-label={t('common.close')} onClick={onClose}>
            <X size={16} aria-hidden="true" />
          </button>
        </header>
        <div className="panel-modal-body">{children}</div>
        {footer !== undefined ? <div className="panel-modal-foot">{footer}</div> : null}
      </div>
    </div>,
    document.body
  );
}
