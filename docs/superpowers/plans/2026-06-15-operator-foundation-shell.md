# Shell-каркас (Этап 0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use `- [ ]`.

**Goal:** Достроить shell-каркас Operator App: «+ Быстрое меню», палитра ⌘K, инфра хоткеев,
сворачиваемая контекст-панель (удаляя заглушку `SummarySidePanel`), строка тревог — каркас с
честными заглушками, наполнение приходит из этапов 1–6.

**Architecture:** 6 новых модулей (`operatorCommands.ts`, `useHotkeys.ts`, `QuickActionsMenu.tsx`,
`CommandPalette.tsx`, `ContextPanel.tsx`, `ShellAlerts.tsx`) + интеграция в `App.tsx`/`styles.css`.
Действия «+» идут через пустой реестр-шов → honest-toast; ⌘K реально навигирует по экранам.

**Tech Stack:** React 19, vanilla CSS (`styles.css`), `@afk4/i18n` ICU, lucide-react, bun test
(happy-dom + @testing-library/react). Запуск: `~/.bun/bin/bun`.

**Источник правды по деталям:** `docs/superpowers/specs/2026-06-15-operator-foundation-shell-design.md`
(читать §-ы по ссылкам ниже). Рабочая директория кода: `src/AFK4.Operator.App.Web`.

**Общие команды (из `src/AFK4.Operator.App.Web`):**
- Тест одного файла: `~/.bun/bin/bun test src/<file>.test.tsx`
- Все тесты Operator: `~/.bun/bin/bun test`
- Типы: `~/.bun/bin/bun run tsc --noEmit` (или `bunx tsc --noEmit`)
- Build: `~/.bun/bin/bun run build`
- i18n regen (из `packages/i18n`): `~/.bun/bin/bun run gen`

---

### Task 1: i18n-ключи + regen

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (корень репо)
- Generated: `packages/i18n/src/messages.ts` (через `bun run gen`, НЕ редактировать руками)

Спек §7 — полный список ключей. Реальный таджикский, НЕ копии ru (#38).

- [ ] **Step 1: grep эксклюзивности `op.summary.*`.** Из корня репо:
  `grep -rn "op.summary" src/AFK4.Operator.App.Web/src` — убедиться, что эти ключи используются
  ТОЛЬКО в `SummarySidePanel.tsx`. Ключи, встречающиеся ещё где-то, НЕ удалять. Список кандидатов
  на удаление — в спеке §7 (titlePc/titleShift/detailsLabel/stateActive/inProgress/actionsCount/
  localData/openAction/titleBooking/titlePos/titlePlayers/titlePayments/titleLogs/titleReview).
- [ ] **Step 2: добавить новые ключи** в ru/en/tg (одинаковые пути, разный перевод). Группы:
  - `op.command.menuLabel`, `op.command.menuButton`
  - `op.command.group.sessions_clients`, `op.command.group.money_stock`
  - `op.command.action.start_session|sell_product|create_player|new_reservation|top_up_wallet|cash_in_order|cash_out_order|stock_intake`
  - `op.command.stage.map|cashier|players|booking|management`
  - `op.command.deferred` — ICU: ru `Действие появится в этапе «{stage}»`, en
    `This action ships in the {stage} stage`, tg `Ин амал дар марҳилаи «{stage}» пайдо мешавад`
  - `op.command.palette.placeholder|label|navHeading|entitySoon|empty|close`
  - `op.context.collapse`, `op.context.expand`
  - `op.alerts.label`, `op.alerts.summary` — ICU plural по проблемам/offline. Пример ru:
    `{problems, plural, =0 {Тревог нет} one {# проблема} few {# проблемы} other {# проблем}} · {offline} офлайн`.
    На каждый язык — свои plural-формы (tg/en тоже валидный ICU, не копия ru).
- [ ] **Step 3: удалить** подтверждённо-эксклюзивные `op.summary.*` из всех трёх locale-файлов.
- [ ] **Step 4: regen.** `cd packages/i18n && ~/.bun/bin/bun run gen`. Убедиться, что
  `messages.ts` обновился без ошибок.
- [ ] **Step 5: tsc** из `src/AFK4.Operator.App.Web`: `bunx tsc --noEmit` — `MessageKey` типы
  валидны. (Здесь возможны ошибки в `SummarySidePanel.tsx` на удалённые ключи — это нормально, он
  удаляется в Task 6; если мешает, можно временно оставить ключи и удалить их в Task 6. Решение
  имплементера, но финально мёртвые ключи должны уйти.)
- [ ] **Step 6: commit** — `i18n(operator): keys for shell command layer, palette, context, alerts`.

---

### Task 2: `operatorCommands.ts` + тест

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/operatorCommands.ts`
- Test: `src/AFK4.Operator.App.Web/src/operatorCommands.test.ts`

Спек §1. Чистые данные + Map-реестр, без React.

- [ ] **Step 1: тест (failing).** `operatorCommands.test.ts`:
```ts
import { describe, expect, it, mock } from 'bun:test';
import { quickActions, getVisibleQuickActions, createCommandRegistry } from './operatorCommands';
import type { OperatorAuthSession } from './authClient';

const session = (perms: string[]) => ({ permissions: perms } as unknown as OperatorAuthSession);

describe('quickActions data', () => {
  it('declares exactly 8 actions with unique ids', () => {
    expect(quickActions).toHaveLength(8);
    expect(new Set(quickActions.map((a) => a.id)).size).toBe(8);
  });
});

describe('getVisibleQuickActions', () => {
  it('keeps only actions whose permission the session holds', () => {
    const visible = getVisibleQuickActions(session(['players.create']));
    expect(visible.map((a) => a.id)).toEqual(['create_player']);
  });
  it('returns nothing for a permission-less session', () => {
    expect(getVisibleQuickActions(session([]))).toHaveLength(0);
  });
});

describe('createCommandRegistry', () => {
  it('dispatches to a registered handler and reports true', () => {
    const reg = createCommandRegistry();
    const handler = mock(() => {});
    reg.register('sell_product', handler);
    expect(reg.dispatch('sell_product')).toBe(true);
    expect(handler).toHaveBeenCalledTimes(1);
  });
  it('returns false when no handler is registered', () => {
    expect(createCommandRegistry().dispatch('sell_product')).toBe(false);
  });
});
```
- [ ] **Step 2: run → fail.** `~/.bun/bin/bun test src/operatorCommands.test.ts`
- [ ] **Step 3: реализация** по таблице спека §1: тип `QuickActionId`, `QuickActionGroup`,
  интерфейс `QuickAction`, массив `quickActions` (8 шт, импортируя строки прав из
  `permissionNames`), `getVisibleQuickActions(session)` через `hasPermission`,
  `createCommandRegistry()` на `Map<QuickActionId, QuickActionHandler>`.
- [ ] **Step 4: run → pass.** Тот же тест.
- [ ] **Step 5: commit** — `feat(operator): quick-action catalog + command registry seam`.

---

### Task 3: `useHotkeys.ts` + тест

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/useHotkeys.ts`
- Test: `src/AFK4.Operator.App.Web/src/useHotkeys.test.ts`

Спек §2.

- [ ] **Step 1: тест (failing).** Рендер тестового компонента с `useHotkeys`, диспатч
  `KeyboardEvent` на `window`:
```ts
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render } from '@testing-library/react';
import { useHotkeys } from './useHotkeys';

afterEach(cleanup);

function Harness({ onK, allowInInputs = true }: { onK: () => void; allowInInputs?: boolean }) {
  useHotkeys([{ key: 'k', ctrl: true, onTrigger: onK, allowInInputs }]);
  return <input data-testid="field" />;
}

function press(init: KeyboardEventInit & { target?: EventTarget }) {
  const ev = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
  (init.target ?? window).dispatchEvent(ev);
}

describe('useHotkeys', () => {
  it('fires when the combo matches', () => {
    const onK = mock(() => {});
    render(<Harness onK={onK} />);
    press({ key: 'k', ctrlKey: true });
    expect(onK).toHaveBeenCalledTimes(1);
  });
  it('ignores key repeat', () => {
    const onK = mock(() => {});
    render(<Harness onK={onK} />);
    press({ key: 'k', ctrlKey: true, repeat: true });
    expect(onK).not.toHaveBeenCalled();
  });
  it('does not fire from an input when allowInInputs is false', () => {
    const onK = mock(() => {});
    const { getByTestId } = render(<Harness onK={onK} allowInInputs={false} />);
    press({ key: 'k', ctrlKey: true, target: getByTestId('field') });
    expect(onK).not.toHaveBeenCalled();
  });
  it('removes its listener on unmount', () => {
    const onK = mock(() => {});
    const { unmount } = render(<Harness onK={onK} />);
    unmount();
    press({ key: 'k', ctrlKey: true });
    expect(onK).not.toHaveBeenCalled();
  });
});
```
- [ ] **Step 2: run → fail.**
- [ ] **Step 3: реализация.** `useHotkeys(bindings)`: один `useEffect` с обработчиком `keydown` на
  `window`; матч `key` (case-insensitive) + модификаторы (`ctrl`/`alt`/`shift`/`meta`; для отсутствующих
  во binding флагов сравнивать с `false`, кроме случаев когда binding явно требует true). Игнор
  `e.repeat`. Если `!allowInInputs` и `e.target` — поле ввода (`INPUT`/`TEXTAREA`/`SELECT`/
  `isContentEditable`) → пропуск. На матче — `e.preventDefault()` + `onTrigger(e)`. Зависимость
  эффекта — `bindings` (документировать, что массив должен быть стабилен/мемоизирован у вызывающего).
- [ ] **Step 4: run → pass.**
- [ ] **Step 5: commit** — `feat(operator): global hotkey hook`.

---

### Task 4: `QuickActionsMenu.tsx` + тест

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/QuickActionsMenu.tsx`
- Test: `src/AFK4.Operator.App.Web/src/QuickActionsMenu.test.tsx`

Спек §3. Зависит от Task 2 (`operatorCommands`).

**Контракт пропсов:** `{ session: OperatorAuthSession | null; onSelect: (action: QuickAction) => void }`.
Кнопка скрыта, если `getVisibleQuickActions(session)` пуст.

- [ ] **Step 1: тест (failing).** Обернуть в `<I18nProvider>`. Кейсы:
  - менеджер-сессия (`['sessions.start','pos.sales.create','players.create','reservations.manage',
    'billing.wallet.top_up','shifts.cash.manage','inventory.stock.manage']`) → открыть меню (клик по
    кнопке `aria-label`=menuLabel), видно ≥6 пунктов, есть оба заголовка групп.
  - кассир-сессия (`['pos.sales.create']`) → в меню только «Продажа товара», складских пунктов нет.
  - выбор пункта (клик) зовёт `onSelect` с правильным `action.id`.
  - Esc закрывает меню.
  - нулевые права → кнопка `queryByRole('button', {name: menuLabel})` отсутствует.
  Использовать `screen.getByText(t('op.command.action.sell_product'))` — но `t` в тесте нет; брать
   russian-строки через рендер `I18nProvider` по умолчанию (локаль ru) и матчить по видимому тексту,
  как в `ShopOrdersWorkspace.test.tsx` (образец рядом).
- [ ] **Step 2: run → fail.**
- [ ] **Step 3: реализация.** Кнопка-триггер (`Plus` icon) + поповер `role="menu"`; группировка по
  `action.group` с заголовками; пункты `role="menuitem"` с лейблом + `<kbd>{hotkeyHint}</kbd>` (если
  не `—`). Клавиатура: открытие Enter/Space/клик; ArrowDown/Up по видимым пунктам; Enter/Space —
  `onSelect`; Esc — закрыть + вернуть фокус на триггер; клик-снаружи — закрыть (через
  `useEffect`+document listener или overlay). Фокус на первый пункт при открытии. Состояния
  hover/focus-visible/active — классами (CSS в Task 8).
- [ ] **Step 4: run → pass.**
- [ ] **Step 5: commit** — `feat(operator): quick-actions «+» menu`.

---

### Task 5: `CommandPalette.tsx` + тест

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/CommandPalette.tsx`
- Test: `src/AFK4.Operator.App.Web/src/CommandPalette.test.tsx`

Спек §4. Использует `navSections` (`operatorData`) + `canOpenWorkspace` (`operatorPermissions`).

**Контракт:** `{ session; onNavigate: (id: WorkspaceId) => void; onClose: () => void }`. Компонент —
сам оверлей (рендерится, когда открыт; открытием/закрытием рулит App). Образец оверлея —
`AccountPanel.tsx` (`.account-panel-overlay` + клик-снаружи + `role="dialog"` `aria-modal`). Добавить
Esc и автофокус на поле.

- [ ] **Step 1: тест (failing).** В `<I18nProvider>`:
  - рендер с менеджер-сессией → видно поле (`aria-label`=palette.label) и заголовок навигации.
  - ввод подстроки в поле фильтрует список переходов; Enter на активной строке зовёт `onNavigate`.
  - Esc зовёт `onClose`.
  - присутствует некликабельный блок `palette.entitySoon` (`screen.getByText(...)`,
    проверить, что это не `button`/нет onClick — напр. тег не интерактивный).
  - права: кассир-сессия (`['pos.sales.create']`) НЕ показывает экран «Управление».
- [ ] **Step 2: run → fail.**
- [ ] **Step 3: реализация.** Оверлей; поле автофокус; список целей из `navSections.flatMap(items)`
  отфильтрованный `canOpenWorkspace(session, id)` И подстрокой запроса по локализованному лейблу;
  группировка по секции; `role="listbox"`/`option`, `aria-activedescendant`; ArrowUp/Down + Enter →
  `onNavigate(id)` затем `onClose()`; Esc/клик-снаружи → `onClose`. Пустой фильтр → строка
  `palette.empty`. Внизу — приглушённый некликабельный блок `palette.entitySoon`.
- [ ] **Step 4: run → pass.**
- [ ] **Step 5: commit** — `feat(operator): ⌘K command palette (navigation)`.

---

### Task 6: `ContextPanel.tsx` + тест, удалить `SummarySidePanel`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ContextPanel.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ContextPanel.test.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/SummarySidePanel.tsx`

Спек §5. (Интеграция с гридом `App.tsx`/CSS — в Task 8; здесь сам компонент-обёртка.)

**Контракт:** `{ collapsed: boolean; onToggle: () => void; title?: string; children: ReactNode }`.
Сам компонент рендерит `aside.context-panel`; когда `collapsed` — рисует тонкую полосу с кнопкой
«развернуть» (`op.context.expand`); когда нет — кнопку «свернуть» (`op.context.collapse`) + children.
Управление персистом/наличием контента — снаружи (App, Task 8), чтобы компонент остался чистым.

- [ ] **Step 1: тест (failing).**
  - collapsed=false → видны children и кнопка с aria `op.context.collapse`; клик зовёт `onToggle`.
  - collapsed=true → children скрыты, видна кнопка `op.context.expand`; клик зовёт `onToggle`.
- [ ] **Step 2: run → fail.**
- [ ] **Step 3: реализация** компонента (без localStorage — это в App). Удалить
  `SummarySidePanel.tsx`. Если на него остался импорт в `App.tsx` — он чинится в Task 8; здесь
  достаточно удалить файл и его тест, если был.
- [ ] **Step 4: run → pass** (`ContextPanel.test.tsx`). Полный `bun test` может временно падать на
  `App.tsx` (импорт удалённого) — это закрывает Task 8.
- [ ] **Step 5: commit** — `feat(operator): collapsible ContextPanel; drop SummarySidePanel stub`.

---

### Task 7: `ShellAlerts.tsx` + тест

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ShellAlerts.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ShellAlerts.test.tsx`

Спек §6.

**Контракт:** `{ problems: number; offline: number }` (шов `sources?` — заложить тип, не
использовать). Danger-класс при `problems > 0`.

- [ ] **Step 1: тест (failing).** В `<I18nProvider>`:
  - `problems=2 offline=1` → элемент имеет класс `danger` (или `data-tone="danger"`), число видно.
  - `problems=0` → нет danger-класса; нулевой текст из `op.alerts.summary`.
- [ ] **Step 2: run → fail.**
- [ ] **Step 3: реализация.** `span.shell-alerts` (+`.danger` при problems>0), иконка (lucide
  `TriangleAlert`/`MonitorCheck`) + `t('op.alerts.summary', { problems, offline })`. Не только цвет —
  иконка меняется/подпись есть.
- [ ] **Step 4: run → pass.**
- [ ] **Step 5: commit** — `feat(operator): shell alerts indicator`.

---

### Task 8: интеграция в `App.tsx` + `styles.css` + финальные гейты

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

Спек §5/§8. Сшивает всё; здесь же грид и персист.

- [ ] **Step 1: App — командный слой.** Создать `const registry = useMemo(() => createCommandRegistry(), [])`.
  Достать `const toast = useToast()`. Вставить в `.top-command` `<QuickActionsMenu session={authSession}
  onSelect={(a) => { if (!registry.dispatch(a.id)) toast.info(t('op.command.deferred', { stage: t(a.stageKey) })); }} />`.
- [ ] **Step 2: App — палитра ⌘K.** Состояние `const [paletteOpen, setPaletteOpen] = useState(false)`.
  `useHotkeys([{ key: 'k', ctrl: true, meta: true... }])` — открыть палитру. ВНИМАНИЕ к matcher: нужно
  ловить и Ctrl+K, и Cmd+K. Простейшее — два биндинга или matcher `ctrl||meta`; если хук строго по
  флагам, передать два биндинга `{key:'k',ctrl:true,...}` и `{key:'k',meta:true,...}`,
  `allowInInputs:true`. Бывший мёртвый инпут `.command-search` → сделать кнопкой/onClick=
  `setPaletteOpen(true)` (оставить вид «поиск», но это триггер). Рендер `{paletteOpen && <CommandPalette
  session={authSession} onNavigate={(id)=>{setWorkspace(id); setPaletteOpen(false);}} onClose={()=>setPaletteOpen(false)} />}`.
- [ ] **Step 3: App — контекст-панель + персист.** Состояние свёрнутости с персистом:
  `const [contextCollapsed, setContextCollapsed] = useState(() => localStorage.getItem('afk4.operator.contextCollapsed') === '1')`;
  `toggle` пишет в localStorage. Определить `hasContextContent` = `workspace === 'map' && selectedSeat !== null`
  (сейчас только карта даёт контент). Заменить нынешний рендер `MapSidePanel` и весь длинный
  `workspace !== ... && <SummarySidePanel/>` на:
  `{hasContextContent && <ContextPanel collapsed={contextCollapsed} onToggle={toggleContext}><MapSidePanel …/></ContextPanel>}`.
  Убрать импорт `SummarySidePanel`.
- [ ] **Step 4: App — грид-переменная.** На `.operator-shell` добавить инлайн-стиль
  `'--shell-context-col': hasContextContent ? (contextCollapsed ? 'var(--shell-context-strip)' : 'minmax(260px,292px)') : '0px'`
  (вместе с существующим `--shell-tabstrip`).
- [ ] **Step 5: App — тревоги.** Заменить инлайновый `<MonitorCheck …>`-span в `.signals-strip` на
  `<ShellAlerts problems={countProblems(displayedFloorMap.seats)} offline={countByTone(displayedFloorMap.seats, 'offline')} />`.
- [ ] **Step 6: styles.css.** Спек §8: `.operator-shell` — 3-я колонка через
  `var(--shell-context-col, minmax(260px,292px))`; объявить `--shell-context-strip: 36px`. Стили
  `.quick-actions-menu*`, `.command-palette*` (по образцу `.account-panel*`), `.context-panel-collapsed`
  (+chevron, переходы `--duration-fast`), `.shell-alerts`/`.shell-alerts.danger`. Все состояния
  (hover/focus-visible/active/disabled), только токены, light/dark паритет, prefers-reduced-motion
  (глобальный гаситель уже есть).
- [ ] **Step 7: гейты.** Из `src/AFK4.Operator.App.Web`:
  `~/.bun/bin/bun test` (всё зелёное, включая старые `App.test.tsx`/`operatorVisibility.test.ts`),
  `bunx tsc --noEmit`, `~/.bun/bin/bun run build`. Чинить корень любых падений (#39), не обходить.
- [ ] **Step 8: commit** — `feat(operator): mount shell command layer, palette, context panel, alerts`.

---

## Self-Review (выполнено при написании)

- **Покрытие спека:** §1→T2, §2→T3, §3→T4, §4→T5, §5→T6+T8, §6→T7+T8, §7→T1, §8→T8. ✓
- **Плейсхолдеры:** тест-код приведён для всех модулей; интеграция расписана по шагам. ✓
- **Согласованность типов:** `QuickAction`/`QuickActionId`/`createCommandRegistry`/`getVisibleQuickActions`
  едины между T2 и потребителями (T4/T8); `ContextPanel` контракт `{collapsed,onToggle,title?,children}`
  един между T6 и T8. ✓
