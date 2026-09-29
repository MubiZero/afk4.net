import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react';
import { MoreHorizontal } from 'lucide-react';
import { IconButton } from './Button';
import { cx } from './cx';

// Одно действие в меню «⋯». `danger` рисует пункт красным и, если перед ним есть обычные,
// отбивает разделителем: опасное не должно стоять вплотную к рутинному.
export interface RowAction {
  id: string;
  label: string;
  icon?: ReactNode;
  onSelect: () => void;
  danger?: boolean;
  disabled?: boolean;
}

// Меню действий строки или карточки: одна кнопка следующего шага остаётся на виду, остальное —
// здесь. Перенесено из Панели (RowActionsMenu), где у него была полная клавиатура: кнопка с
// aria-haspopup/expanded, список role=menu, фокус на первый пункт при открытии, стрелки и
// Home/End по кругу, Escape и клик мимо закрывают, Tab закрывает, фокус возвращается на «⋯».
// Пустой список — ничего не рисуется: кнопка, за которой пусто, обещает то, чего нет.
export function RowActions({ actions, label, size = 'md' }: { actions: RowAction[]; label: string; size?: 'sm' | 'md' }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const firstDangerIndex = actions.findIndex((action) => action.danger);
  const separatorBeforeIndex = firstDangerIndex > 0 ? firstDangerIndex : -1;

  useLayoutEffect(() => {
    if (open) itemRefs.current[0]?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
    };
    const onPointerDown = (event: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) close();
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
    if (action.disabled) return;
    close();
    action.onSelect();
  };

  const move = (event: ReactKeyboardEvent, index: number) => {
    const last = actions.length - 1;
    const targets: Record<string, number> = { ArrowDown: index === last ? 0 : index + 1, ArrowUp: index === 0 ? last : index - 1, Home: 0, End: last };
    const target = targets[event.key];
    if (event.key === 'Tab') {
      event.preventDefault();
      close();
    } else if (target !== undefined) {
      event.preventDefault();
      itemRefs.current[target]?.focus();
    }
  };

  if (actions.length === 0) return null;

  return (
    <div className="ui-menu" ref={rootRef}>
      <IconButton
        ref={triggerRef}
        label={label}
        size={size}
        icon={<MoreHorizontal size={16} aria-hidden="true" />}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={(event) => {
          event.stopPropagation();
          setOpen((current) => !current);
        }}
      />
      {open && (
        <div className="ui-menu-list" role="menu" aria-label={label}>
          {actions.map((action, index) => (
            <Fragment key={action.id}>
              {index === separatorBeforeIndex && <div className="ui-menu-sep" role="separator" />}
              <button
                ref={(node) => { itemRefs.current[index] = node; }}
                type="button"
                role="menuitem"
                className={cx('ui-menu-item', action.danger && 'is-danger')}
                tabIndex={-1}
                disabled={action.disabled}
                onClick={(event) => { event.stopPropagation(); select(action); }}
                onKeyDown={(event) => move(event, index)}
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
