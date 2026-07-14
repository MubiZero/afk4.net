# Operator App — Этап 0 «Фундамент», кусок «Shell-каркас»

**Дата:** 2026-06-15
**Статус:** дизайн на ревью
**Охват:** `src/AFK4.Operator.App.Web` — последний кусок Этапа 0 (§1 + §9 п.3 спека фундамента
`2026-06-14-operator-foundation-design.md`)

## Контекст

Этап 0 «Фундамент» делается по кускам. Уже в `main`: «Деньги», «Видимость роль→секции»,
«Примитивы» (Toast/Skeleton/EmptyState). Остался **последний кусок — shell-каркас**: верхняя
полоса с «**+ Быстрое меню**», слот **⌘K** (командная палитра), инфраструктура хоткеев,
**сворачиваемая контекст-панель** и **строка тревог** в статус-баре. Он же закрывает техдолг
`SummarySidePanel.tsx` (статичная заглушка с фейковыми суммами).

**Ключевой принцип куска (из §1 фундамента):** строим **каркас**, а **наполнение** приходит из
этапов 1–6. То есть командный слой, палитра, хоткеи, панель и тревоги должны быть полноценными
по механике (клавиатура, фокус, права, состояния — #32), но реальные бизнес-флоу действий «+» и
индексация сущностей ⌘K **намеренно отложены** и подключаются позже через явные швы. Заглушки —
**честные** (#37/#38): не делают вид, что работают.

**Что уже частично есть в коде (оживляем, не плодим):**
- В верхней полосе (`App.tsx`) — **мёртвый** инпут поиска (`.command-search`, ничего не делает).
  Он становится триггером палитры ⌘K.
- В статус-баре (`.signals-strip`) — уже считаются offline/проблемы по карте
  (`countByTone`/`countProblems`). Это зачаток строки тревог — формализуем.
- `SummarySidePanel` рендерится сейчас ровно для одного воркспейса (`payment_cards`) как статичная
  заглушка — удаляем.

**Раскладка shell (как есть):** CSS-grid `.operator-shell` — колонки `74px | 1fr |
minmax(260px,292px)` (рейл · рабочая · контекст), строки `46px | 1fr | 26px` (верх-полоса · тело ·
строка-сигналов). Контекст-панель — колонка 3.

## Решения (развилки закрыты с пользователем 2026-06-15)

1. **«+» заглушки → активны + дис-патч через реестр, ненаписанные → toast «скоро (этап X)».**
   Меню полное (все доступные по правам пункты), с клавиатурой и хоткей-подсказками. Каждый пункт
   идёт через **реестр команд** (`createCommandRegistry`). В фундаменте реестр пуст → любой выбор
   даёт `toast.info('Действие появится в этапе «<Название этапа>»')`. Этапы 1–6 потом регистрируют
   реальные обработчики — UI и права уже готовы.
2. **⌘K → рабочая навигация-палитра, поиск сущностей помечен «скоро».** Переход по 6 секциям и
   доступным экранам работает реально сейчас (данные есть: `navSections` + права). Блок «поиск
   клиентов / ПК / чеков» виден как **явно отложенный** (disabled-подсказка), не фейкается.
3. **Контекст-панель → shell-уровневый сворачиваемый регион; `SummarySidePanel`-заглушка удалена.**
   Нет контента у экрана → зона свёрнута (без фейк-цифр). Карта сохраняет свой `MapSidePanel` как
   контент панели. Состояние «свёрнуто» у экрана с контентом запоминается в `localStorage`.

## 1. Командный слой данных — `operatorCommands.ts` (новый)

Единый источник данных о действиях «+» и шов диспетчеризации. **Только данные + тонкий реестр**,
без бизнес-логики (она в этапах).

```ts
export type QuickActionId =
  | 'start_session' | 'sell_product' | 'create_player' | 'new_reservation'
  | 'top_up_wallet' | 'cash_in_order' | 'cash_out_order' | 'stock_intake';

export type QuickActionGroup = 'sessions_clients' | 'money_stock';

export interface QuickAction {
  id: QuickActionId;
  labelKey: MessageKey;       // op.command.action.<id>
  group: QuickActionGroup;
  permission: string;          // из permissionNames — фильтр видимости пункта
  hotkeyHint: string;          // отображаемая подсказка, напр. 'Ctrl+Alt+Z' (провизорная, §3 фунд.)
  stageKey: MessageKey;        // op.command.stage.<этап> — для honest-заглушки
}

export const quickActions: QuickAction[] = [ /* 8 шт, см. таблицу */ ];
```

**Таблица 8 действий** (порядок = порядок в меню; группы — заголовки в меню):

| id | labelKey | group | permission | hotkeyHint | stage (поставщик) |
|---|---|---|---|---|---|
| `start_session` | Запустить сессию | sessions_clients | `sessions.start` | Ctrl+Alt+Z | Карта |
| `sell_product` | Продажа товара | sessions_clients | `pos.sales.create` | Ctrl+Alt+S | Касса |
| `create_player` | Создать клиента | sessions_clients | `players.create` | Ctrl+Alt+N | Клиенты |
| `new_reservation` | Новая бронь | sessions_clients | `reservations.manage` | Ctrl+Alt+B | Брони |
| `top_up_wallet` | Пополнить депозит | money_stock | `billing.wallet.top_up` | Ctrl+Alt+D | Клиенты |
| `cash_in_order` | Приходный ордер | money_stock | `shifts.cash.manage` | — | Касса |
| `cash_out_order` | Расходный ордер | money_stock | `shifts.cash.manage` | — | Касса |
| `stock_intake` | Внесение на склад | money_stock | `inventory.stock.manage` | — | Управление |

**Реестр-шов:**

```ts
export type QuickActionHandler = () => void;

export interface CommandRegistry {
  register(id: QuickActionId, handler: QuickActionHandler): void;
  dispatch(id: QuickActionId): boolean; // true если был обработчик
}

export function createCommandRegistry(): CommandRegistry { /* Map-based */ }
```

В `App.tsx` создаётся один пустой реестр. `QuickActionsMenu` зовёт `onSelect(action)`; App делает:
`if (!registry.dispatch(action.id)) toast.info(t('op.command.deferred', { stage: t(action.stageKey) }))`.
Так наполнение этапов — это просто `registry.register(...)`, без касания каркаса (#16/#19/#29).

`getVisibleQuickActions(session)` — фильтр по `hasPermission(session, action.permission)`.

## 2. Инфраструктура хоткеев — `useHotkeys.ts` (новый)

Минимальный переиспользуемый хук: один глобальный `keydown`-слушатель на массив биндингов.

```ts
export interface HotkeyBinding {
  key: string;           // 'k', 'z', ... (case-insensitive)
  ctrl?: boolean; alt?: boolean; shift?: boolean; meta?: boolean;
  onTrigger: (e: KeyboardEvent) => void;
  allowInInputs?: boolean; // по умолчанию false: не срабатывать при фокусе в input/textarea/select/[contenteditable]
}
export function useHotkeys(bindings: HotkeyBinding[]): void;
```

- `Ctrl+K` и `Cmd+K` оба открывают палитру (matcher: `ctrl || meta`) — `allowInInputs: true`
  (палитра должна открываться даже из поля).
- 8 quick-action-комбо биндятся через тот же хук и идут в **тот же дис-патч** (реестр → toast).
  Это честно демонстрирует, что инфра работает, не фейкая сами флоу.
- Cleanup слушателя в `useEffect`-return. Матч игнорирует автоповтор (`e.repeat`).

> YAGNI: не строим «глобальный реестр хоткеев с подсказками-оверлеем» — хватает хука + статичных
> `hotkeyHint` в данных. Богатый cheat-sheet — если реально понадобится в этапах.

## 3. «+ Быстрое меню» — `QuickActionsMenu.tsx` (новый)

Кнопка «+» в верхней полосе + выпадающее меню.

- **Триггер:** кнопка `aria-haspopup="menu"`, `aria-expanded`. Иконка `Plus` (lucide) + подпись.
- **Меню:** `role="menu"`, пункты `role="menuitem"`; сгруппированы (2 группы с заголовками
  `op.command.group.sessions_clients` / `op.command.group.money_stock`). Каждый пункт: иконка +
  лейбл + хоткей-подсказка справа (`<kbd>`).
- **Фильтр прав:** только `getVisibleQuickActions(session)`. Пустая группа не рисует заголовок.
  Если доступных действий нет вообще — кнопка «+» скрыта.
- **Клавиатура:** открытие по клику/Enter/Space; ArrowDown/ArrowUp — перемещение по пунктам (по
  видимым, через границы групп), Enter/Space — выбор, Esc — закрыть и вернуть фокус на «+»,
  Tab/клик-снаружи — закрыть. Фокус при открытии — на первый пункт.
- **Выбор** → `onSelect(action)` (App → реестр/toast).

## 4. Командная палитра ⌘K — `CommandPalette.tsx` (новый)

Оверлей в стиле проекта (паттерн `AccountPanel`: `.command-palette-overlay` + клик-снаружи закрыть;
`role="dialog"` `aria-modal="true"`). Добавляет к паттерну Esc и управление фокусом.

- **Открытие:** хоткей ⌘K/Ctrl+K (через `useHotkeys`) и клик по полю в верх-полосе (бывший мёртвый
  инпут). Esc / клик-снаружи — закрыть.
- **Поле поиска:** автофокус при открытии; `aria-label`. Фильтрует список переходов по подстроке.
- **Навигация (реально работает):** список целей = доступные экраны (`navSections` → `items`,
  отфильтрованные `canOpenWorkspace(session, id)`), сгруппированные по секциям. ArrowUp/Down —
  движение, Enter — `onNavigate(workspaceId)` + закрыть. Подсветка активной строки.
- **Поиск сущностей — явно отложен:** отдельный блок внизу с подписью
  `op.command.palette.entitySoon` («Поиск клиентов, ПК и чеков появится позже») — некликабельный,
  приглушённый. Не фейкаем результаты (#37).
- **Пустой результат** фильтра по навигации → `EmptyState`/строка «ничего не найдено»
  (`op.command.palette.empty`).
- A11y: `role="listbox"`/`option` для списка, `aria-activedescendant` на активной строке.

## 5. Сворачиваемая контекст-панель — `ContextPanel.tsx` (новый) + удаление `SummarySidePanel`

Shell-уровневая обёртка правой зоны. Заменяет прямой рендер `MapSidePanel`/`SummarySidePanel`.

- **Пропсы:** `{ title?: string; collapsible?: boolean; children?: ReactNode }`. Контент даёт
  воркспейс (сейчас только Карта → `MapSidePanel`); остальные экраны контент не дают.
- **Три состояния зоны** (App вычисляет и кладёт CSS-переменную ширины колонки на `.operator-shell`):
  - **есть контент + развёрнуто** → полная ширина (`minmax(260px,292px)`), панель + кнопка
    «свернуть» (chevron, `aria-expanded`).
  - **есть контент + свёрнуто** → тонкая полоса (`var(--shell-context-strip, 36px)`) с кнопкой
    «развернуть». Выбор пользователя сохраняется в `localStorage` (`afk4.operator.contextCollapsed`).
  - **нет контента** → колонка схлопнута в `0` (зона полностью отдаёт место рабочей области). Без
    фейк-цифр.
- **Грид:** `.operator-shell` получает `--shell-context-col` (вместо хардкода 3-й колонки);
  `grid-template-columns: 74px minmax(0,1fr) var(--shell-context-col)`. App ставит переменную:
  `0` / `36px` / `minmax(260px,292px)`.
- `SummarySidePanel.tsx` и его i18n-ключи (`op.summary.*`, использованные **только** там) удаляются;
  условие рендера в `App.tsx` (длинный `workspace !== ...`) уходит. Карта рендерит
  `<ContextPanel collapsible><MapSidePanel …/></ContextPanel>`.

> Перед удалением `op.summary.*` ключей — проверить grep по репо, что они не используются вне
> `SummarySidePanel`. Используемые где-то ещё (`op.shopOrders.title`, `nav.settings`,
> `payments_cards.nav`, `op.loyalty.title`, `op.news.title`, `op.shifts.title`) **не трогать** —
> они принадлежат другим экранам.

## 6. Строка тревог — `ShellAlerts.tsx` (новый, лёгкий)

Формализует уже считающиеся offline/проблемы в выделенный, расширяемый элемент строки сигналов.

- Вход: `{ problems: number; offline: number }` (из `countProblems`/`countByTone`, как сейчас).
- Тон: спокойный при `problems === 0`; **danger-акцент** при `problems > 0` (линза #30/#31:
  тревога должна выделяться). Кодируется не только цветом — иконка + число + подпись.
- **Шов для этапа «Карта»:** компонент принимает массив источников тревог опционально
  (`sources?: AlertSource[]`), сейчас не передаётся — наполнение позже. Каркас просто показывает
  агрегат.
- Живёт в `.signals-strip`, заменяя текущий инлайновый `<MonitorCheck …>`-span.

## 7. i18n (ru/en/tg, реальный таджикский — #38)

Новые ключи (затем `cd packages/i18n && bun run gen`):

- `op.command.menuLabel` («Быстрое меню» / aria), `op.command.menuButton` («Создать» или «+»).
- `op.command.group.sessions_clients`, `op.command.group.money_stock`.
- `op.command.action.<id>` × 8.
- `op.command.stage.map|cashier|players|booking|management` (названия этапов для заглушки).
- `op.command.deferred` (ICU: «Действие появится в этапе «{stage}»»).
- `op.command.palette.placeholder`, `op.command.palette.label`, `op.command.palette.navHeading`,
  `op.command.palette.entitySoon`, `op.command.palette.empty`, `op.command.palette.close`.
- `op.context.collapse`, `op.context.expand` (aria кнопок).
- `op.alerts.label`, `op.alerts.summary` (ICU plural по problems/offline; **на каждый язык свои
  плюрал-формы**, не копии — #37/#38).

Удаляемые (если grep подтверждает эксклюзивность `SummarySidePanel`): `op.summary.titlePc`,
`op.summary.titleShift`, `op.summary.detailsLabel`, `op.summary.stateActive`, `op.summary.inProgress`,
`op.summary.actionsCount`, `op.summary.localData`, `op.summary.openAction`, `op.summary.titleBooking`,
`op.summary.titlePos`, `op.summary.titlePlayers`, `op.summary.titlePayments`, `op.summary.titleLogs`,
`op.summary.titleReview` — и только реально-эксклюзивные.

## 8. CSS (`styles.css`)

- `.operator-shell` — заменить хардкод 3-й колонки на `var(--shell-context-col, minmax(260px,292px))`.
- «+» меню: кнопка-триггер в `.top-command`; `.quick-actions-menu` (поповер, `surface-elevated`,
  тень, группы, `<kbd>`-подсказки), hover/**focus-visible**/active/disabled — полный набор (#32).
- Палитра: `.command-palette-overlay` + `.command-palette` (по образцу `.account-panel-*`), список
  `.command-palette-option` с активной/hover/**focus-visible**, отложенный блок приглушён.
- Контекст-панель: `.context-panel-collapsed` (тонкая полоса + кнопка-chevron), переходы по
  `--duration-fast`, уважать `prefers-reduced-motion` (глобальный гаситель уже есть).
- Тревоги: `.shell-alerts` + `.shell-alerts.danger` (токен `--danger-*`, не hex — #6.4).

Только токены, без hex в `.tsx`. Light/dark паритет.

## 9. Файлы

**Создать:** `operatorCommands.ts`, `useHotkeys.ts`, `QuickActionsMenu.tsx`, `CommandPalette.tsx`,
`ContextPanel.tsx`, `ShellAlerts.tsx` (+ тесты на каждый: `*.test.ts(x)`).
**Изменить:** `App.tsx` (монтаж всего, реестр+toast, ⌘K-хоткей, замена инпута/панели/тревог),
`styles.css`, `locales/{ru,en,tg}.json` → `packages/i18n` regen.
**Удалить:** `SummarySidePanel.tsx` (+ мёртвые `op.summary.*` ключи).

## 10. Тесты (bun, happy-dom + testing-library)

- `operatorCommands.test.ts`: фильтр прав (`getVisibleQuickActions`), реестр (зарегистрированный
  обработчик зовётся и `dispatch` → true; незарегистрированный → false).
- `useHotkeys.test.ts`: комбо срабатывает; игнор при фокусе в input (если не `allowInInputs`);
  cleanup снимает слушатель; `e.repeat` игнорируется.
- `QuickActionsMenu.test.tsx`: рисует только разрешённые действия; ролевой кейс (кассир не видит
  складских); клавиатура (стрелки/Enter/Esc); выбор зовёт `onSelect`; кнопка скрыта при нуле прав.
- `CommandPalette.test.tsx`: открытие/Esc; фильтр навигации по запросу + правам; Enter → navigate;
  блок «поиск сущностей — скоро» присутствует и некликабелен.
- `ContextPanel.test.tsx`: разворот/сворот + персист в `localStorage`; нет контента → свёрнуто/0;
  контент рендерится.
- `ShellAlerts.test.tsx`: счётчики; danger-тон при `problems>0`; спокойный нулевой стейт.
- Регресс: `App.test.tsx`, `operatorVisibility.test.ts` — зелёные (каркас не ломает навигацию/права).

## 11. Готово, когда

- «+» в верх-полосе: меню фильтруется правами, работает клавиатура и хоткеи, выбор ненаписанного
  действия даёт honest-toast «появится в этапе X»; реестр-шов готов для этапов.
- ⌘K открывает палитру (хоткей + клик по полю), реально переходит по доступным экранам с
  клавиатуры; поиск сущностей явно помечен «скоро».
- Контекст-панель сворачивается/разворачивается с персистом; `SummarySidePanel`-заглушка и её
  мёртвые ключи удалены; нет контента → зона отдаёт место.
- Строка тревог — выделенный элемент с danger-тоном на проблемах, со швом для «Карты».
- Гейты зелёные: `bun test` (Operator, включая новые тесты), `tsc`, `bun run build`. Старые экраны
  не сломаны.
