import { useEffect, useId, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { X } from 'lucide-react';

interface SheetProps {
  title: string;
  onClose: () => void;
  /** Пока идёт денежное действие, закрыть нельзя: игрок должен увидеть, чем оно кончилось. */
  closeDisabled?: boolean;
  children: ReactNode;
}

/** Лист поверх экрана сессии — продление, ранний выход. Esc и крестик закрывают, фон не кликабелен. */
export function Sheet({ title, onClose, closeDisabled = false, children }: SheetProps) {
  const { t } = useI18n();
  const titleId = useId();

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !closeDisabled) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [closeDisabled, onClose]);

  return (
    <div className="sheet-backdrop">
      <section className="sheet" role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <header className="sheet__head">
          <h2 id={titleId} className="sheet__title">{title}</h2>
          <button type="button" className="sheet__close" onClick={onClose} disabled={closeDisabled} aria-label={t('playerShell.sheet.close')}>
            <X aria-hidden="true" />
          </button>
        </header>
        {children}
      </section>
    </div>
  );
}
