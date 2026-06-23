import { useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { MoreHorizontal, Pencil, KeyRound, Power, PowerOff } from 'lucide-react';

// Меню «⋯» действий с клиентом в шапке карточки. a11y: кнопка с aria-haspopup/aria-expanded,
// список role=menu/menuitem, закрытие по Escape и клику вне. Гейтинг по праву — выше (ClientDetail).
export function ClientActionsMenu({
  isActive,
  onEditProfile,
  onSetPin,
  onToggleActive,
}: {
  isActive: boolean;
  onEditProfile: () => void;
  onSetPin: () => void;
  onToggleActive: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };
    const onPointer = (event: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('pointerdown', onPointer);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('pointerdown', onPointer);
    };
  }, [open]);

  const select = (handler: () => void) => {
    setOpen(false);
    handler();
  };

  return (
    <div className="client-actions-menu" ref={rootRef}>
      <button
        type="button"
        className="client-actions-trigger"
        aria-label={t('op.players.menu.open')}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        <MoreHorizontal size={16} aria-hidden="true" />
      </button>
      {open && (
        <div className="client-actions-dropdown" role="menu">
          <button type="button" role="menuitem" className="client-actions-item" onClick={() => select(onEditProfile)}>
            <Pencil size={14} aria-hidden="true" />
            {t('op.players.actions.editProfileLabel')}
          </button>
          <button type="button" role="menuitem" className="client-actions-item" onClick={() => select(onSetPin)}>
            <KeyRound size={14} aria-hidden="true" />
            {t('op.players.actions.pinLabel')}
          </button>
          <button
            type="button"
            role="menuitem"
            className={`client-actions-item${isActive ? ' is-danger' : ''}`}
            onClick={() => select(onToggleActive)}
          >
            {isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />}
            {isActive ? t('op.players.actions.deactivateLabel') : t('op.players.actions.reactivateLabel')}
          </button>
        </div>
      )}
    </div>
  );
}
