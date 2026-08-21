import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { CalendarClock, MoreHorizontal, Pencil, Power, PowerOff, SlidersHorizontal } from 'lucide-react';

interface MenuAction {
  key: string;
  label: string;
  icon: ReactNode;
  onSelect: () => void;
  danger?: boolean;
}

// Меню «⋯» действий с клиентом в шапке карточки/drawer'а. Пункты собираются динамически по
// правам — «Бронь»/«Корректировка» гейтятся СВОИМИ флагами независимо от canManageClient
// (оператор может иметь право на корректировку без права редактировать профиль). Пустое меню
// (ни одного разрешённого пункта) не рендерится вовсе — вызывающий код должен решить, показывать
// ли триггер «⋯», по тому же условию (canManageClient || canCreateReservation || canCorrect).
// a11y: кнопка с aria-haspopup/aria-expanded, список role=menu/menuitem, закрытие по Escape
// (document-уровень) и клику вне, автофокус первого пункта, навигация стрелками (roving focus),
// возврат фокуса на триггер при закрытии, закрытие по Tab.
export function ClientActionsMenu({
  isActive,
  canManageClient,
  onEditProfile,
  onToggleActive,
  canCreateReservation = false,
  onCreateReservation,
  canCorrect = false,
  onCorrect,
}: {
  isActive: boolean;
  canManageClient: boolean;
  onEditProfile: () => void;
  onToggleActive: () => void;
  canCreateReservation?: boolean;
  onCreateReservation?: () => void;
  canCorrect?: boolean;
  onCorrect?: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const showReservation = canCreateReservation && Boolean(onCreateReservation);
  const showCorrection = canCorrect && Boolean(onCorrect);

  const items: MenuAction[] = [];
  if (showReservation) {
    items.push({
      key: 'reservation',
      label: t('op.players.actions.bookingBtn'),
      icon: <CalendarClock size={14} aria-hidden="true" />,
      onSelect: onCreateReservation!,
    });
  }
  if (canManageClient) {
    items.push({
      key: 'edit',
      label: t('op.players.actions.editProfileLabel'),
      icon: <Pencil size={14} aria-hidden="true" />,
      onSelect: onEditProfile,
    });
  }
  if (showCorrection) {
    items.push({
      key: 'correction',
      label: t('op.players.correction.openLink'),
      icon: <SlidersHorizontal size={14} aria-hidden="true" />,
      onSelect: onCorrect!,
    });
  }
  // Деактивация — опасное действие, отделяем визуально от остальных пунктов разделителем
  // (только если перед ней есть что отделять).
  const separatorBeforeIndex = canManageClient && items.length > 0 ? items.length : -1;
  if (canManageClient) {
    items.push({
      key: 'toggle',
      label: isActive ? t('op.players.actions.deactivateLabel') : t('op.players.actions.reactivateLabel'),
      icon: isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />,
      onSelect: onToggleActive,
      danger: isActive,
    });
  }

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
      itemRefs.current[(index + 1) % items.length]?.focus();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      itemRefs.current[(index - 1 + items.length) % items.length]?.focus();
    } else if (event.key === 'Home') {
      event.preventDefault();
      itemRefs.current[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault();
      itemRefs.current[items.length - 1]?.focus();
    }
  };

  if (items.length === 0) {
    return null;
  }

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
          {items.map((item, index) => (
            <Fragment key={item.key}>
              {index === separatorBeforeIndex && <div className="client-actions-sep" role="separator" />}
              <button
                ref={(node) => { itemRefs.current[index] = node; }}
                type="button"
                role="menuitem"
                className={`client-actions-item${item.danger ? ' is-danger' : ''}`}
                tabIndex={-1}
                onClick={() => select(item.onSelect)}
                onKeyDown={(event) => onItemKeyDown(event, index)}
              >
                {item.icon}
                {item.label}
              </button>
            </Fragment>
          ))}
        </div>
      )}
    </div>
  );
}
