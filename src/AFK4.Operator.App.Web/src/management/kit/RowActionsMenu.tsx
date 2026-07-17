import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { MoreHorizontal } from 'lucide-react';
import type { RowAction } from './types';

// Универсальное ⋯-меню действий строки/записи — обобщение players/ClientActionsMenu, чтобы
// каждый раздел «Управления» не переписывал a11y-дропдаун заново. Пустой список действий →
// не рендерится (вызывающий сам решает, показывать ли триггер). Опасные пункты (`danger`)
// группируются как есть в переданном порядке; разделитель ставится перед первым danger-пунктом,
// если до него есть обычные. a11y: кнопка с aria-haspopup/expanded, список role=menu/menuitem,
// закрытие по Escape (document) и клику вне, автофокус первого пункта, стрелки (roving focus),
// возврат фокуса на триггер при закрытии, закрытие по Tab.
export function RowActionsMenu({ actions, ariaLabel }: { actions: RowAction[]; ariaLabel?: string }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const firstDangerIndex = actions.findIndex((action) => action.danger);
  const separatorBeforeIndex = firstDangerIndex > 0 ? firstDangerIndex : -1;

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
    triggerRef.current?.focus();
  };

  const select = (action: RowAction) => {
    if (action.disabled) {
      return;
    }
    close();
    action.onSelect();
  };

  const onItemKeyDown = (event: ReactKeyboardEvent, index: number) => {
    if (event.key === 'Tab') {
      event.preventDefault();
      close();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      itemRefs.current[(index + 1) % actions.length]?.focus();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      itemRefs.current[(index - 1 + actions.length) % actions.length]?.focus();
    } else if (event.key === 'Home') {
      event.preventDefault();
      itemRefs.current[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault();
      itemRefs.current[actions.length - 1]?.focus();
    }
  };

  if (actions.length === 0) {
    return null;
  }

  return (
    <div className="mgmt-menu" ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className="mgmt-menu-trigger"
        aria-label={ariaLabel ?? t('op.management.crud.rowMenu')}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={(event) => {
          event.stopPropagation();
          setOpen((current) => !current);
        }}
      >
        <MoreHorizontal size={16} aria-hidden="true" />
      </button>
      {open && (
        <div className="mgmt-menu-dropdown" role="menu">
          {actions.map((action, index) => (
            <Fragment key={action.id}>
              {index === separatorBeforeIndex && <div className="mgmt-menu-sep" role="separator" />}
              <button
                ref={(node) => { itemRefs.current[index] = node; }}
                type="button"
                role="menuitem"
                className={`mgmt-menu-item${action.danger ? ' is-danger' : ''}`}
                tabIndex={-1}
                disabled={action.disabled}
                onClick={(event) => { event.stopPropagation(); select(action); }}
                onKeyDown={(event) => onItemKeyDown(event, index)}
              >
                {action.icon}
                {action.label}
              </button>
            </Fragment>
          ))}
        </div>
      )}
    </div>
  );
}
