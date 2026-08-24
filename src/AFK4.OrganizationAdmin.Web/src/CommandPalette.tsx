import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { navSections } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext, WorkspaceId } from './operatorTypes';
import type { OperatorAuthSession } from './authClient';

/// Строка палитры: либо экран, либо человек. Список один и плоский — стрелки ходят сквозь
/// оба раздела, потому что для того, кто набирает, это один список, а не два.
type PaletteOption =
  | { kind: 'nav'; key: string; label: string; workspaceId: WorkspaceId }
  | { kind: 'person'; key: string; label: string; hint: string | null; playerAccountId: string };

/// Кого нашли по набранному: пусто, ищем, нашли или не смогли.
type PeopleState =
  | { status: 'off' }
  | { status: 'searching' }
  | { status: 'ready'; people: PaletteOption[] }
  | { status: 'failed' };

// Максимум строк в палитре — чтобы окно не переполнялось; остальное отсекаем с подсказкой.
const MAX_VISIBLE = 8;

// Людей показываем немного: палитра — это «отвези меня к нему», а не список клиентов.
// Кому нужен список — тому нужен раздел «Клиенты» с фильтрами.
const MAX_PEOPLE = 5;

// Одна буква совпала бы с половиной клубной базы, и каждая следующая гоняла бы сеть впустую.
const MIN_PEOPLE_QUERY = 2;

// Столько же, сколько ждёт поиск в самом разделе клиентов: набор идёт быстрее, чем ответ сети.
const PEOPLE_DEBOUNCE_MS = 200;

export function CommandPalette({ session, backend, visibleWorkspaceIds, onNavigate, onOpenPerson, onClose }: {
  session: OperatorAuthSession | null;
  // Нужен для поиска людей: палитра спрашивает клиентов того же филиала, в котором смена.
  // null — работа без бэкенда (фикстуры): тогда людей палитра не ищет.
  backend?: OperatorBackendContext | null;
  // Extra restriction on top of ordinary permission checks — a support session's writableAreas
  // (see support/supportWorkspaces.ts). `null`/omitted outside support mode: permissions alone decide.
  visibleWorkspaceIds?: ReadonlySet<WorkspaceId> | null;
  onNavigate: (id: WorkspaceId) => void;
  // Открыть карточку человека. Не задан — палитра людей не ищет (так её зовут тесты соседних
  // экранов, которым нужен только переход по разделам).
  onOpenPerson?: (person: { playerAccountId: string; search: string }) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Плоский список разрешённых экранов с уже локализованными подписями — фильтруем по подстроке.
  const allowed = useMemo<PaletteOption[]>(() => {
    return navSections
      .flatMap((section) => section.items)
      .filter((item) => canOpenWorkspace(session, item.id) && (visibleWorkspaceIds == null || visibleWorkspaceIds.has(item.id)))
      .map((item) => ({ kind: 'nav' as const, key: `nav:${item.id}`, label: t(item.labelKey), workspaceId: item.id }));
  }, [session, visibleWorkspaceIds, t]);

  const filtered = useMemo<PaletteOption[]>(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return allowed;
    return allowed.filter((target) => target.label.toLowerCase().includes(needle));
  }, [allowed, query]);

  // Показываем не весь список, а первые MAX_VISIBLE — иначе окно переполняется. Остаток не прячем
  // молча: подсказываем уточнить запрос (#34). Навигация/выбор работают по видимому срезу.
  const visibleNav = filtered.slice(0, MAX_VISIBLE);
  const hiddenCount = filtered.length - visibleNav.length;

  // Людей ищем только там, где карточку клиента вообще можно открыть: право на раздел и
  // (в режиме поддержки) разрешение видеть его. Иначе палитра стала бы обходом прав.
  const peopleSearchable =
    backend != null &&
    onOpenPerson != null &&
    canOpenWorkspace(session, 'players') &&
    (visibleWorkspaceIds == null || visibleWorkspaceIds.has('players'));

  const [people, setPeople] = useState<PeopleState>({ status: 'off' });
  const needle = query.trim();
  const lookingForPeople = peopleSearchable && needle.length >= MIN_PEOPLE_QUERY;

  const platformBaseUrl = backend?.config.platformBaseUrl;
  const accessToken = backend?.session.accessToken;
  const branchId = backend?.branchId;

  useEffect(() => {
    if (!lookingForPeople || backend == null) {
      setPeople({ status: 'off' });
      return undefined;
    }

    let disposed = false;
    setPeople({ status: 'searching' });
    const timer = window.setTimeout(() => {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      clients.players
        .searchPlayers(backend.branchId, needle, MAX_PEOPLE)
        .then((found) => {
          if (disposed) return;
          setPeople({
            status: 'ready',
            people: found.map((person) => ({
              kind: 'person' as const,
              key: `person:${person.playerAccountId}`,
              label: person.displayName,
              // Номер телефона — то, чем людей и различают: тёзок в клубной базе больше, чем
              // кажется, и без номера палитра предлагала бы выбрать из двух одинаковых строк.
              hint: person.phoneNumber,
              playerAccountId: person.playerAccountId
            }))
          });
        })
        .catch(() => {
          if (!disposed) setPeople({ status: 'failed' });
        });
    }, PEOPLE_DEBOUNCE_MS);

    return () => {
      disposed = true;
      window.clearTimeout(timer);
    };
    // Сессия и филиал в зависимостях: смена смены или филиала меняет, у кого спрашивать.
  }, [needle, lookingForPeople, platformBaseUrl, accessToken, branchId]);

  const visiblePeople = people.status === 'ready' ? people.people : [];
  const options = [...visibleNav, ...visiblePeople];

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    // Держим активную строку в пределах видимого среза.
    setActiveIndex((index) => Math.min(index, Math.max(0, options.length - 1)));
  }, [options.length]);

  const optionId = (index: number) => `command-palette-option-${index}`;
  const navListboxId = 'command-palette-listbox';
  const peopleListboxId = 'command-palette-people-listbox';

  function choose(option: PaletteOption) {
    if (option.kind === 'nav') {
      onNavigate(option.workspaceId);
    } else {
      // Раздел клиентов ищет по той же строке, что набрали здесь: человек должен оказаться в
      // списке, из которого его карточку и открывают.
      onOpenPerson?.({ playerAccountId: option.playerAccountId, search: needle });
    }
    onClose();
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((index) => Math.min(index + 1, Math.max(0, options.length - 1)));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      const target = options[activeIndex];
      if (target) {
        choose(target);
      }
    }
  }

  const renderOption = (option: PaletteOption, index: number) => (
    <li
      key={option.key}
      id={optionId(index)}
      role="option"
      aria-selected={index === activeIndex}
      className={index === activeIndex ? 'command-palette-option is-active' : 'command-palette-option'}
      onMouseEnter={() => setActiveIndex(index)}
      onClick={() => choose(option)}
    >
      {option.label}
      {option.kind === 'person' && option.hint && (
        <span className="command-palette-option-hint">{option.hint}</span>
      )}
    </li>
  );

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
          aria-expanded={options.length > 0}
          aria-controls={filtered.length ? navListboxId : undefined}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          aria-label={t('op.command.palette.label')}
          placeholder={peopleSearchable
            ? t('op.command.palette.placeholderWithPeople')
            : t('op.command.palette.placeholder')}
          aria-activedescendant={options.length ? optionId(activeIndex) : undefined}
        />
        <div className="command-palette-groups">
          <div className="command-palette-group">
            <p className="command-palette-heading">{t('op.command.palette.navHeading')}</p>
            {filtered.length === 0 ? (
              <p className="command-palette-empty">{t('op.command.palette.empty')}</p>
            ) : (
              <ul id={navListboxId} className="command-palette-list" role="listbox">
                {visibleNav.map((option, index) => renderOption(option, index))}
              </ul>
            )}
            {hiddenCount > 0 && (
              <p className="command-palette-more">{t('op.command.palette.more', { count: hiddenCount })}</p>
            )}
          </div>

          {lookingForPeople && (
            <div className="command-palette-group">
              <p className="command-palette-heading">{t('op.command.palette.peopleHeading')}</p>
              {people.status === 'searching' && (
                <p className="command-palette-empty">{t('op.command.palette.peopleSearching')}</p>
              )}
              {people.status === 'failed' && (
                <p className="command-palette-empty">{t('op.command.palette.peopleFailed')}</p>
              )}
              {people.status === 'ready' && visiblePeople.length === 0 && (
                <p className="command-palette-empty">{t('op.command.palette.peopleEmpty')}</p>
              )}
              {visiblePeople.length > 0 && (
                <ul id={peopleListboxId} className="command-palette-list" role="listbox">
                  {visiblePeople.map((option, index) => renderOption(option, visibleNav.length + index))}
                </ul>
              )}
            </div>
          )}
        </div>
        <p className="command-palette-soon">{t('op.command.palette.entitySoon')}</p>
      </div>
    </div>
  );
}
