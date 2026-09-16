import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { navSections } from './operatorData';
import { canOpenWorkspace, hasPermission, permissionNames } from './operatorPermissions';
import { createAuthenticatedOperatorClients, formatDateTime, formatMinorUnits } from './operatorHelpers';
import type { BranchSearchResultDto } from './operatorApiClients';
import type { OperatorBackendContext, WorkspaceId } from './operatorTypes';
import type { OperatorAuthSession } from './authClient';

/// Строка палитры: либо экран, либо найденная сущность. Список один и плоский — стрелки ходят
/// сквозь все разделы, потому что для того, кто набирает, это один список, а не пять.
type PaletteOption =
  | { kind: 'nav'; key: string; label: string; workspaceId: WorkspaceId }
  | { kind: 'entity'; key: string; label: string; hint: string | null; entity: BranchSearchResultDto };

/// Что нашли по набранному: пусто, ищем, нашли или не смогли.
type SearchState =
  | { status: 'off' }
  | { status: 'searching' }
  | { status: 'ready'; results: BranchSearchResultDto[] }
  | { status: 'failed' };

// Максимум строк-экранов в палитре — чтобы окно не переполнялось; остальное отсекаем с подсказкой.
const MAX_VISIBLE = 8;

// Находок каждого вида показываем немного: палитра — это «отвези меня туда», а не список.
// Кому нужен список — тому нужен раздел с фильтрами.
const MAX_PER_KIND = 5;

// Одна буква совпала бы с половиной клубной базы, и каждая следующая гоняла бы сеть впустую.
const MIN_QUERY = 2;

// Столько же, сколько ждёт поиск в самом разделе клиентов: набор идёт быстрее, чем ответ сети.
const SEARCH_DEBOUNCE_MS = 200;

/// Виды находок в том порядке, в каком они полезны за стойкой: место под рукой, потом человек,
/// потом бронь, потом чек. Каждый вид показываем только тому, кому этот раздел и так открыт, —
/// иначе палитра стала бы обходом прав.
const ENTITY_KINDS = [
  { kind: 'seat', headingKey: 'op.command.palette.seatsHeading', workspaceId: 'map', permission: permissionNames.viewFloorMap },
  { kind: 'player', headingKey: 'op.command.palette.peopleHeading', workspaceId: 'players', permission: permissionNames.viewPlayers },
  { kind: 'reservation', headingKey: 'op.command.palette.reservationsHeading', workspaceId: 'booking', permission: permissionNames.viewReservations },
  { kind: 'receipt', headingKey: 'op.command.palette.receiptsHeading', workspaceId: 'cash', permission: permissionNames.viewReceipt }
] as const satisfies readonly { kind: string; headingKey: MessageKey; workspaceId: WorkspaceId; permission: string }[];

export type PaletteReservationTarget = { reservationId: string; startsAtUtc: string | null };

export function CommandPalette({
  session,
  backend,
  visibleWorkspaceIds,
  onNavigate,
  onOpenPerson,
  onOpenSeat,
  onOpenReservation,
  onOpenReceipt,
  onClose
}: {
  session: OperatorAuthSession | null;
  // Нужен для поиска сущностей: палитра спрашивает тот филиал, в котором идёт смена.
  // null — работа без бэкенда (фикстуры): тогда палитра ищет только экраны.
  backend?: OperatorBackendContext | null;
  // Extra restriction on top of ordinary permission checks — a support session's writableAreas
  // (see support/supportWorkspaces.ts). `null`/omitted outside support mode: permissions alone decide.
  visibleWorkspaceIds?: ReadonlySet<WorkspaceId> | null;
  onNavigate: (id: WorkspaceId) => void;
  // Открыть найденное. Обработчик не задан — вид не ищется: строка, которая никуда не ведёт,
  // хуже отсутствующей.
  onOpenPerson?: (person: { playerAccountId: string; search: string }) => void;
  onOpenSeat?: (seatId: string) => void;
  onOpenReservation?: (target: PaletteReservationTarget) => void;
  onOpenReceipt?: (target: { receiptId: string }) => void;
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

  const openers: Record<string, ((entity: BranchSearchResultDto) => void) | undefined> = {
    seat: onOpenSeat ? (entity) => onOpenSeat(entity.id) : undefined,
    player: onOpenPerson ? (entity) => onOpenPerson({ playerAccountId: entity.id, search: query.trim() }) : undefined,
    reservation: onOpenReservation
      ? (entity) => onOpenReservation({ reservationId: entity.id, startsAtUtc: entity.occursAtUtc ?? null })
      : undefined,
    receipt: onOpenReceipt ? (entity) => onOpenReceipt({ receiptId: entity.id }) : undefined
  };

  // Ищем только то, что этому человеку и так видно и есть чем открыть.
  const searchableKinds = ENTITY_KINDS.filter((entry) =>
    openers[entry.kind] != null &&
    canOpenWorkspace(session, entry.workspaceId) &&
    hasPermission(session, entry.permission) &&
    (visibleWorkspaceIds == null || visibleWorkspaceIds.has(entry.workspaceId)));
  const searchableKindNames = searchableKinds.map((entry) => entry.kind).join(',');

  const [search, setSearch] = useState<SearchState>({ status: 'off' });
  const needle = query.trim();
  const searching = backend != null && searchableKinds.length > 0 && needle.length >= MIN_QUERY;

  const platformBaseUrl = backend?.config.platformBaseUrl;
  const accessToken = backend?.session.accessToken;
  const branchId = backend?.branchId;

  useEffect(() => {
    if (!searching || backend == null) {
      setSearch({ status: 'off' });
      return undefined;
    }

    let disposed = false;
    setSearch({ status: 'searching' });
    const timer = window.setTimeout(() => {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      clients.search
        .searchBranch(backend.branchId, needle, MAX_PER_KIND)
        .then((found) => {
          if (disposed) return;
          setSearch({ status: 'ready', results: found });
        })
        .catch(() => {
          if (!disposed) setSearch({ status: 'failed' });
        });
    }, SEARCH_DEBOUNCE_MS);

    return () => {
      disposed = true;
      window.clearTimeout(timer);
    };
    // Сессия и филиал в зависимостях: смена смены или филиала меняет, у кого спрашивать.
  }, [needle, searching, searchableKindNames, platformBaseUrl, accessToken, branchId]);

  // Подпись строки: то, чем находку узнают глазами. Время и деньги форматирует клиент — язык
  // и часовой пояс знает он, а не сервер.
  const hintOf = (entity: BranchSearchResultDto): string | null => {
    const parts: string[] = [];
    if (entity.kind === 'receipt' && entity.amountMinorUnits != null) {
      parts.push(formatMinorUnits(entity.amountMinorUnits, entity.currencyCode ?? ''));
    }

    if (entity.subtitle) {
      parts.push(entity.subtitle);
    }

    if (entity.occursAtUtc) {
      parts.push(formatDateTime(entity.occursAtUtc));
    }

    return parts.length > 0 ? parts.join(' · ') : null;
  };

  const found = search.status === 'ready' ? search.results : [];
  const groups = searchableKinds
    .map((entry) => ({
      ...entry,
      options: found
        .filter((entity) => entity.kind === entry.kind)
        .map<PaletteOption>((entity) => ({
          kind: 'entity',
          key: `${entity.kind}:${entity.id}`,
          label: entity.title,
          hint: hintOf(entity),
          entity
        }))
    }))
    .filter((group) => group.options.length > 0);

  const options = [...visibleNav, ...groups.flatMap((group) => group.options)];
  // Смещение, с которого начинается каждая группа в плоском списке: строки нумеруются сквозь
  // все разделы, иначе стрелки и подсветка разъезжаются.
  const groupOffsets = groups.reduce<number[]>((offsets, _group, index) => {
    const previous = index === 0 ? visibleNav.length : offsets[index - 1]! + groups[index - 1]!.options.length;
    offsets.push(previous);
    return offsets;
  }, []);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    // Держим активную строку в пределах видимого среза.
    setActiveIndex((index) => Math.min(index, Math.max(0, options.length - 1)));
  }, [options.length]);

  const optionId = (index: number) => `command-palette-option-${index}`;
  const navListboxId = 'command-palette-listbox';

  function choose(option: PaletteOption) {
    if (option.kind === 'nav') {
      onNavigate(option.workspaceId);
    } else {
      openers[option.entity.kind]?.(option.entity);
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
      {option.kind === 'entity' && option.hint && (
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
          placeholder={searchableKinds.length > 0
            ? t('op.command.palette.placeholderWithEntities')
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

          {searching && (
            <>
              {search.status === 'searching' && (
                <p className="command-palette-empty">{t('op.command.palette.searching')}</p>
              )}
              {search.status === 'failed' && (
                <p className="command-palette-empty">{t('op.command.palette.searchFailed')}</p>
              )}
              {search.status === 'ready' && groups.length === 0 && (
                <p className="command-palette-empty">{t('op.command.palette.entityEmpty')}</p>
              )}
              {groups.map((group, groupIndex) => (
                <div className="command-palette-group" key={group.kind}>
                  <p className="command-palette-heading">{t(group.headingKey)}</p>
                  <ul id={`command-palette-${group.kind}-listbox`} className="command-palette-list" role="listbox">
                    {group.options.map((option, index) => renderOption(option, groupOffsets[groupIndex]! + index))}
                  </ul>
                </div>
              ))}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
