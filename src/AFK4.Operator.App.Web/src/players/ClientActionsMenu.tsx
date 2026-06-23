import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { MoreHorizontal, Pencil, KeyRound, Power, PowerOff } from 'lucide-react';

// Меню «⋯» действий с клиентом в шапке карточки. a11y: кнопка с aria-haspopup/aria-expanded,
// список role=menu/menuitem, закрытие по Escape (document-уровень) и клику вне,
// автофокус первого пункта, навигация стрелками (roving focus),
// возврат фокуса на триггер при закрытии, закрытие по Tab.
// Гейтинг по праву — выше (ClientDetail).
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
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const ITEM_COUNT = 3;

  // Автофокус первого пункта при открытии.
  useLayoutEffect(() => {
    if (open) {
      itemRefs.current[0]?.focus();
    }
  }, [open]);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        close();
      }
    };
    const onPointerDown = (event: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        close();
      }
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('pointerdown', onPointerDown, true);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('pointerdown', onPointerDown, true);
    };
  }, [open]);

  const close = () => {
    setOpen(false);
    // Возвращаем фокус на триггер при любом закрытии.
    triggerRef.current?.focus();
  };

  const select = (handler: () => void) => {
    close();
    handler();
  };

  const onItemKeyDown = (event: ReactKeyboardEvent, index: number) => {
    if (event.key === 'Tab') {
      event.preventDefault();
      close();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      itemRefs.current[(index + 1) % ITEM_COUNT]?.focus();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      itemRefs.current[(index - 1 + ITEM_COUNT) % ITEM_COUNT]?.focus();
    } else if (event.key === 'Home') {
      event.preventDefault();
      itemRefs.current[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault();
      itemRefs.current[ITEM_COUNT - 1]?.focus();
    }
  };

  return (
    <div className="client-actions-menu" ref={rootRef}>
      <button
        ref={triggerRef}
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
          <button
            ref={(node) => { itemRefs.current[0] = node; }}
            type="button"
            role="menuitem"
            className="client-actions-item"
            tabIndex={-1}
            onClick={() => select(onEditProfile)}
            onKeyDown={(event) => onItemKeyDown(event, 0)}
          >
            <Pencil size={14} aria-hidden="true" />
            {t('op.players.actions.editProfileLabel')}
          </button>
          <button
            ref={(node) => { itemRefs.current[1] = node; }}
            type="button"
            role="menuitem"
            className="client-actions-item"
            tabIndex={-1}
            onClick={() => select(onSetPin)}
            onKeyDown={(event) => onItemKeyDown(event, 1)}
          >
            <KeyRound size={14} aria-hidden="true" />
            {t('op.players.actions.pinLabel')}
          </button>
          <button
            ref={(node) => { itemRefs.current[2] = node; }}
            type="button"
            role="menuitem"
            className={`client-actions-item${isActive ? ' is-danger' : ''}`}
            tabIndex={-1}
            onClick={() => select(onToggleActive)}
            onKeyDown={(event) => onItemKeyDown(event, 2)}
          >
            {isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />}
            {isActive ? t('op.players.actions.deactivateLabel') : t('op.players.actions.reactivateLabel')}
          </button>
        </div>
      )}
    </div>
  );
}
