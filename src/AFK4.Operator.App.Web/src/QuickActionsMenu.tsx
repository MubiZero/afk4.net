import { useEffect, useRef, useState } from 'react';
import { Plus } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import {
  getVisibleQuickActions,
  type QuickAction,
  type QuickActionGroup
} from './operatorCommands';

const GROUP_ORDER: QuickActionGroup[] = ['sessions_clients', 'money_stock'];

export function QuickActionsMenu({
  session,
  onSelect
}: {
  session: OperatorAuthSession | null;
  onSelect: (action: QuickAction) => void;
}): JSX.Element | null {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<HTMLButtonElement[]>([]);

  const visible = getVisibleQuickActions(session);

  // Flat list of visible actions in render order, used for arrow-key navigation.
  const orderedActions = GROUP_ORDER.flatMap((group) =>
    visible.filter((action) => action.group === group)
  );

  useEffect(() => {
    if (!open) {
      return;
    }
    itemRefs.current[0]?.focus();
    const handleMouseDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (menuRef.current?.contains(target) || triggerRef.current?.contains(target)) {
        return;
      }
      setOpen(false);
    };
    document.addEventListener('mousedown', handleMouseDown);
    return () => document.removeEventListener('mousedown', handleMouseDown);
  }, [open]);

  if (visible.length === 0) {
    return null;
  }

  const close = (returnFocus: boolean) => {
    setOpen(false);
    if (returnFocus) {
      triggerRef.current?.focus();
    }
  };

  const handleTriggerKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      setOpen(true);
    }
  };

  const focusItem = (index: number) => {
    const count = orderedActions.length;
    const next = ((index % count) + count) % count;
    itemRefs.current[next]?.focus();
  };

  const handleMenuKeyDown = (event: React.KeyboardEvent<HTMLDivElement>, index: number) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      close(true);
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      focusItem(index + 1);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      focusItem(index - 1);
    }
  };

  const select = (action: QuickAction) => {
    onSelect(action);
    close(true);
  };

  itemRefs.current = [];

  return (
    <div className="quick-actions">
      <button
        ref={triggerRef}
        type="button"
        className="quick-actions-trigger top-command"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('op.command.menuLabel')}
        onClick={() => setOpen((value) => !value)}
        onKeyDown={handleTriggerKeyDown}
      >
        <Plus size={16} />
        {t('op.command.menuButton')}
      </button>
      {open && (
        <div
          ref={menuRef}
          role="menu"
          className="quick-actions-menu"
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              close(true);
            }
          }}
        >
          {GROUP_ORDER.map((group) => {
            const groupActions = visible.filter((action) => action.group === group);
            if (groupActions.length === 0) {
              return null;
            }
            return (
              <div key={group} className="quick-actions-group">
                <div className="quick-actions-group-header">{t(`op.command.group.${group}`)}</div>
                {groupActions.map((action) => {
                  const index = orderedActions.indexOf(action);
                  return (
                    <button
                      key={action.id}
                      ref={(node) => {
                        if (node) {
                          itemRefs.current[index] = node;
                        }
                      }}
                      type="button"
                      role="menuitem"
                      className="quick-actions-item"
                      onClick={() => select(action)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          select(action);
                          return;
                        }
                        handleMenuKeyDown(event, index);
                      }}
                    >
                      <span>{t(action.labelKey)}</span>
                      {action.hotkeyHint && <kbd>{action.hotkeyHint}</kbd>}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
