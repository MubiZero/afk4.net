import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { LogOut, UserRound } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[1][0]).toUpperCase();
}

/**
 * Единый виджет оператора в подвале рейла: аватар с инициалами раскрывает меню с именем
 * и действиями (профиль, выход). Заменяет текст-ссылку «Оператор смены» в шапке, которая
 * не читалась как кнопка и стояла не на своём месте — место аккаунта внизу боковой панели.
 * Меню рендерим в портал: рейл скроллит с overflow-x:hidden и иначе обрезал бы поповер.
 */
export function RailAccount({ displayName, onOpenAccount, onSignOut }: {
  displayName: string;
  onOpenAccount: () => void;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [coords, setCoords] = useState<{ left: number; bottom: number }>({ left: 0, bottom: 0 });
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onPointerDown(event: PointerEvent) {
      const target = event.target as Node;
      if (triggerRef.current?.contains(target) || menuRef.current?.contains(target)) return;
      setOpen(false);
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') setOpen(false);
    }
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  function toggle() {
    if (open) {
      setOpen(false);
      return;
    }
    const rect = triggerRef.current?.getBoundingClientRect();
    if (rect) {
      setCoords({ left: rect.right + 8, bottom: window.innerHeight - rect.bottom });
    }
    setOpen(true);
  }

  const initials = initialsOf(displayName);

  return (
    <div className="rail-account">
      <button
        type="button"
        ref={triggerRef}
        className="rail-account-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('op.shell.myAccount')}
        onClick={toggle}
      >
        <span className="rail-account-avatar" aria-hidden="true">{initials}</span>
        <span className="rail-account-name">{t('op.shell.account')}</span>
      </button>
      {open && createPortal(
        <div
          ref={menuRef}
          className="rail-account-menu"
          style={{ left: coords.left, bottom: coords.bottom }}
          role="menu"
          aria-label={t('op.shell.myAccount')}
        >
          <div className="rail-account-head">
            <span className="rail-account-avatar lg" aria-hidden="true">{initials}</span>
            <strong>{displayName}</strong>
          </div>
          <button
            type="button"
            role="menuitem"
            className="rail-account-item"
            onClick={() => {
              setOpen(false);
              onOpenAccount();
            }}
          >
            <UserRound size={16} aria-hidden="true" /><span>{t('op.shell.myAccount')}</span>
          </button>
          <button
            type="button"
            role="menuitem"
            className="rail-account-item danger"
            onClick={() => {
              setOpen(false);
              onSignOut();
            }}
          >
            <LogOut size={16} aria-hidden="true" /><span>{t('shell.signOut')}</span>
          </button>
        </div>,
        document.body
      )}
    </div>
  );
}
