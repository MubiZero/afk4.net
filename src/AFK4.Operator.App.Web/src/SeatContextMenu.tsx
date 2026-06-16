import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Ban, BellRing, LockKeyhole, Plus, Power, TimerReset, UnlockKeyhole, Wifi } from 'lucide-react';
import type { ComponentType, KeyboardEvent as ReactKeyboardEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import type { SeatMenuItem, SeatMenuSection } from './seatMenu';

type IconType = ComponentType<{ size?: number; 'aria-hidden'?: boolean }>;

// Иконка пункта по его id — пункт «читается» глазом, не только текстом.
const ITEM_ICON: Record<string, IconType> = {
  'start-guest': Plus,
  'extend-15': Plus,
  'extend-30': TimerReset,
  'pc-lock': LockKeyhole,
  'pc-unlock': UnlockKeyhole,
  'soon-reboot': TimerReset,
  'soon-shutdown': Power,
  'soon-wake': Wifi,
  'soon-fine': Ban,
  'soon-notify': BellRing
};

const MENU_MARGIN = 8;

export function SeatContextMenu({
  seat,
  sections,
  x,
  y,
  onClose,
  onSelect
}: {
  seat: SeatSummary;
  sections: SeatMenuSection[];
  x: number;
  y: number;
  onClose: () => void;
  onSelect: (item: SeatMenuItem) => void;
}) {
  const { t } = useI18n();
  const menuRef = useRef<HTMLDivElement | null>(null);
  const itemRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const [pos, setPos] = useState({ x, y });

  // Плоский список активируемых пунктов — по нему ходит клавиатура (мимо disabled).
  const focusable = useMemo(() => {
    const flat: SeatMenuItem[] = [];
    for (const section of sections) {
      for (const item of section.items) {
        if (!item.disabled) {
          flat.push(item);
        }
      }
    }
    return flat;
  }, [sections]);

  // Кламп в видимую область: меню не должно вылезать за край экрана.
  useLayoutEffect(() => {
    const node = menuRef.current;
    if (node === null) {
      return;
    }

    const rect = node.getBoundingClientRect();
    let nextX = x;
    let nextY = y;
    if (x + rect.width + MENU_MARGIN > window.innerWidth) {
      nextX = Math.max(MENU_MARGIN, window.innerWidth - rect.width - MENU_MARGIN);
    }
    if (y + rect.height + MENU_MARGIN > window.innerHeight) {
      nextY = Math.max(MENU_MARGIN, window.innerHeight - rect.height - MENU_MARGIN);
    }
    if (nextX !== pos.x || nextY !== pos.y) {
      setPos({ x: nextX, y: nextY });
    }
  }, [x, y]);

  // Фокус на первый активный пункт при открытии — меню сразу управляемо с клавиатуры.
  useEffect(() => {
    itemRefs.current[0]?.focus();
  }, []);

  useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node | null)) {
        onClose();
      }
    };
    document.addEventListener('pointerdown', onPointerDown, true);
    return () => document.removeEventListener('pointerdown', onPointerDown, true);
  }, [onClose]);

  const moveFocus = (delta: number, current: number) => {
    if (focusable.length === 0) {
      return;
    }

    const next = (current + delta + focusable.length) % focusable.length;
    itemRefs.current[next]?.focus();
  };

  const onKeyDown = (event: ReactKeyboardEvent, index: number) => {
    if (event.key === 'Escape' || event.key === 'Tab') {
      event.preventDefault();
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      moveFocus(1, index);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      moveFocus(-1, index);
    } else if (event.key === 'Home') {
      event.preventDefault();
      itemRefs.current[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault();
      itemRefs.current[focusable.length - 1]?.focus();
    }
  };

  let focusIndex = -1;
  return (
    <div
      ref={menuRef}
      className="seat-menu"
      role="menu"
      aria-label={t('op.map.menu.label', { seat: seat.name })}
      style={{ left: pos.x, top: pos.y }}
    >
      <header className="seat-menu-head">
        <strong>{seat.name}</strong>
        <span>{seat.stateLabel}</span>
      </header>
      {sections.length === 0 && <p className="seat-menu-empty">{t('op.map.menu.empty')}</p>}
      {sections.map((section) => (
        <div className="seat-menu-section" key={section.id} role="group" aria-label={section.titleKey ? t(section.titleKey) : undefined}>
          {section.titleKey && <span className="seat-menu-section-title">{t(section.titleKey)}</span>}
          {section.items.map((item) => {
            const Icon = ITEM_ICON[item.id];
            const myIndex = item.disabled ? -1 : ++focusIndex;
            return (
              <button
                key={item.id}
                ref={(node) => { if (!item.disabled) { itemRefs.current[myIndex] = node; } }}
                type="button"
                role="menuitem"
                className={`seat-menu-item${item.soon ? ' soon' : ''}`}
                disabled={item.disabled}
                tabIndex={-1}
                onClick={() => onSelect(item)}
                onKeyDown={(event) => onKeyDown(event, myIndex)}
              >
                {Icon && <Icon size={15} aria-hidden={true} />}
                <span className="seat-menu-item-label">{t(item.labelKey)}</span>
                {item.soon && <em className="seat-menu-soon-tag">{t('op.map.menu.soonTag')}</em>}
                {item.hintKey && !item.soon && <em className="seat-menu-item-hint">{t(item.hintKey)}</em>}
              </button>
            );
          })}
        </div>
      ))}
      <footer className="seat-menu-foot">{t('op.map.menu.panelHint')}</footer>
    </div>
  );
}
