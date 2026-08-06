import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { navSections } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import type { WorkspaceId } from './operatorTypes';
import type { OperatorAuthSession } from './authClient';

interface NavTarget {
  id: WorkspaceId;
  label: string;
}

// Максимум строк в палитре — чтобы окно не переполнялось; остальное отсекаем с подсказкой.
const MAX_VISIBLE = 8;

export function CommandPalette({ session, visibleWorkspaceIds, onNavigate, onClose }: {
  session: OperatorAuthSession | null;
  // Extra restriction on top of ordinary permission checks — a support session's writableAreas
  // (see support/supportWorkspaces.ts). `null`/omitted outside support mode: permissions alone decide.
  visibleWorkspaceIds?: ReadonlySet<WorkspaceId> | null;
  onNavigate: (id: WorkspaceId) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Плоский список разрешённых экранов с уже локализованными подписями — фильтруем по подстроке.
  const allowed = useMemo<NavTarget[]>(() => {
    return navSections
      .flatMap((section) => section.items)
      .filter((item) => canOpenWorkspace(session, item.id) && (visibleWorkspaceIds == null || visibleWorkspaceIds.has(item.id)))
      .map((item) => ({ id: item.id, label: t(item.labelKey) }));
  }, [session, visibleWorkspaceIds, t]);

  const filtered = useMemo<NavTarget[]>(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return allowed;
    return allowed.filter((target) => target.label.toLowerCase().includes(needle));
  }, [allowed, query]);

  // Показываем не весь список, а первые MAX_VISIBLE — иначе окно переполняется. Остаток не прячем
  // молча: подсказываем уточнить запрос (#34). Навигация/выбор работают по видимому срезу.
  const visible = filtered.slice(0, MAX_VISIBLE);
  const hiddenCount = filtered.length - visible.length;

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    // Держим активную строку в пределах видимого среза.
    setActiveIndex((index) => Math.min(index, Math.max(0, visible.length - 1)));
  }, [visible.length]);

  const optionId = (index: number) => `command-palette-option-${index}`;
  const listboxId = 'command-palette-listbox';

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((index) => Math.min(index + 1, Math.max(0, visible.length - 1)));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      const target = visible[activeIndex];
      if (target) {
        onNavigate(target.id);
        onClose();
      }
    }
  }

  return (
    <div className="command-palette-overlay" onClick={onClose}>
      <div
        className="command-palette"
        role="dialog"
        aria-modal="true"
        onClick={(event) => event.stopPropagation()}
        onKeyDown={handleKeyDown}
      >
        <input
          ref={inputRef}
          className="command-palette-input"
          type="text"
          role="combobox"
          aria-expanded={filtered.length > 0}
          aria-controls={filtered.length ? listboxId : undefined}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          aria-label={t('op.command.palette.label')}
          placeholder={t('op.command.palette.placeholder')}
          aria-activedescendant={visible.length ? optionId(activeIndex) : undefined}
        />
        <div className="command-palette-group">
          <p className="command-palette-heading">{t('op.command.palette.navHeading')}</p>
          {filtered.length === 0 ? (
            <p className="command-palette-empty">{t('op.command.palette.empty')}</p>
          ) : (
            <ul id={listboxId} className="command-palette-list" role="listbox">
              {visible.map((target, index) => (
                <li
                  key={target.id}
                  id={optionId(index)}
                  role="option"
                  aria-selected={index === activeIndex}
                  className={index === activeIndex ? 'command-palette-option is-active' : 'command-palette-option'}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => {
                    onNavigate(target.id);
                    onClose();
                  }}
                >
                  {target.label}
                </li>
              ))}
            </ul>
          )}
          {hiddenCount > 0 && (
            <p className="command-palette-more">{t('op.command.palette.more', { count: hiddenCount })}</p>
          )}
        </div>
        <p className="command-palette-soon">{t('op.command.palette.entitySoon')}</p>
      </div>
    </div>
  );
}
