# Оператор — единая админка: слияние дублей (под-проект №3)

**Дата:** 2026-07-24
**Эпик:** «Оператор = единая админка» (под-проект №3 из 4)
**Ветка:** `feat/operator-unified-admin-merge-duplicates`
**Предшественники:** №1 Фундамент (merge 93c6f778), №2 Org-экраны (merge 0b6b3055) — оба в main.

---

## 1. Проблема и цель

Эпик поглощает owner-панель Platform.Web `/club` в operator-app, чтобы в №4 Platform.Web/club
можно было **удалить без потери функций**. После №1 и №2 оператор уже почти полный superset
Platform.Web/club — сверка всех 11 экранов `/club` показала лишь **два реальных пробела**:

| Platform.Web /club | В операторе | Статус |
|---|---|---|
| Overview (дашборд филиала) | Отчёты→Обзор (`DashboardWorkspace`) | ✅ покрыто |
| Venue (устройства) | Карта + Управление→Залы и ПК | ✅ покрыто¹ |
| Clients | Клиенты | ✅ покрыто (богаче) |
| Monetization (тарифы/каталог/пакеты) | Управление→Тарифы+Пакеты, Товары | ✅ покрыто |
| Settings (профиль + персонал) | Управление→Клуб, Сотрудники | ✅ покрыто² |
| Branches / Install / Billing | Сеть→Филиалы/Установка/Подписка | ✅ покрыто (№2) |
| Profile (мой профиль) | AccountPanel (кнопка в топ-баре) | ✅ покрыто |
| **Reports (5 отчётов с диапазоном дат)** | разбросано / частично | ⚠️ **пробел** |
| **Journal (аудит филиала)** | в недостижимом `logs` + частично Касса | ⚠️ **пробел** |

¹ Нет drag&drop floor-map редактора и подтверждения новых устройств — **намеренно** отброшено
(грид-only, device-approval отклонён). Зоны/места редактируются CRUD-ом в «Залах». Вне объёма.
² Микро-разница: тумблер «ручное подтверждение устройств» — фича device-approval отклонена. Вне объёма.

**Цель №3:** закрыть два пробела чисто на фронте (бэкенд не меняется — все эндпоинты уже есть),
убрать мёртвый код `logs`, и **сертифицировать паритет дублей**, чтобы №4 удалял безопасно.

---

## 2. Ключевое наблюдение: бэкенд не трогаем

Все нужные эндпоинты и права уже существуют:

- **Отчёты:** `shifts.ts`-клиент уже содержит все 5 отчётов и CSV-экспорты:
  `getShiftReport`, `getSalesReport`, `getGameplayTimeReport`, `getCashOperationReport`,
  `getOperatorActionReport` + соответствующие `export*ReportCsv` → `GET /api/branches/{id}/reports/{name}`
  и `/export.csv`. Гейт — право `reports.view` (уже назначено).
- **Аудит филиала:** `GET /api/branches/{id}/audit` (клиент `audit.search`, используется в Касса→Журнал
  и `logs`). Гейт — право `audit.view` (branch-level). Эндпоинт идёт через `RequireBranchPermissionAsync`
  из фундамента №1 → per-branch проверка, **утечки нет** (в отличие от org-журнала №2, где flat-union
  `RequireOrganizationPermission` потребовал owner-эксклюзивного права).

Backend, права, контракты — **без изменений**. №3 = фронтовая консолидация + удаление dead code.

---

## 3. Объём (согласовано с пользователем)

**Вариант Б — калиброванный, не порт ради порта.** «История» строится не как слепой паритет всех 5
отчётов Platform.Web, а закрывает реальные боли:

- **Сверка кассы за период** (построчно, в приложении): отчёты **Смены** и **Кассовые операции**.
  Сейчас достаются только выгрузкой CSV → открыть в Excel. Это то, чем ловят недостачу у кассира.
- **Два ранее недостижимых отчёта:** **Игровое время** и **Действия операторов** — в операторе UI
  нет вообще, живут только в мёртвом `logs`.
- **Продажи (sales)** — в «Историю» **НЕ включаем**: дашборд-Обзор + чеки в Касса→Журнал уже во
  многом их показывают, а CSV-экспорт продаж остаётся в Касса→Смена. YAGNI.

Итого «История» = **4 отчёта**: Смены, Кассовые операции, Игровое время, Действия операторов.

**Аудит паритета дублей** — верификация (не код): делегированное поэкранное сравнение оператор ≥
Platform.Web. Мелкие дыры чиню в №3; крупные — явно фиксирую и выношу решение. Заведомо отброшенное
(floor-map-редактор, device-approval, тумблер ручного подтверждения) в аудит не включаю.

**Вне объёма:** floor-map drag&drop редактор, подтверждение устройств, тумблеры настроек филиала,
любые изменения бэкенда/прав/контрактов, само удаление Platform.Web/club (это №4).

---

## 4. Архитектура

### 4.1. «Отчёты» → destination-switcher

Секция рейла «Отчёты» (сейчас одиночный воркспейс `dashboard` → `DashboardWorkspace`) становится
destination-switcher по паттерну «Управление»/«Сеть» (`ManagementWorkspace`/`NetworkWorkspace`:
каркас `management-layout` + левый под-рейл `management-nav` + `management-active-pane`).

Новый хост `ReportsWorkspace.tsx` рендерится **на том же `WorkspaceId 'dashboard'`** (id не меняем,
чтобы не трогать роутинг/права/навигацию-по-клику из карточек дашборда — минимум churn). Три
destination (`reportsNav.ts`, по образцу `networkNav.ts`):

| Destination id | Экран | Право (видимость) |
|---|---|---|
| `overview` | Существующий live-дашборд (тело `DashboardWorkspace` переезжает как есть) | `reports.view` |
| `history` | Новый: 4 исторических отчёта, диапазон дат + таблица + CSV | `reports.view` |
| `journal` | Аудит филиала (переиспользование UI из №2, branch-scoped) | `audit.view` |

Видимость destination — `hasAnyPermission` по правам (как в network/management). Union прав секции —
`workspacePermissionRules.dashboard` расширяется до `[reports.view, audit.view]`.

> **Разумное допущение (id `dashboard`):** сохраняем существующий `WorkspaceId 'dashboard'` под новым
> хостом-switcher. Переименование в `'reports'` затронуло бы `operatorTypes`, `operatorPermissions`,
> `WorkspaceRouter` и любые `onNavigate('dashboard')` из карточек — churn без пользы. Внутренняя
> лёгкая нестыковка (id `dashboard` хостит «Отчёты») приемлема; ярлык рейла и заголовки — «Отчёты».

### 4.2. «Обзор» — перенос без изменений логики

Тело текущего `DashboardWorkspace` (live-сводка «сейчас», KPI-плитки, период, sales-CSV, навигация
по клику в `map`/`cash`/`players`) переезжает в destination `overview` **без изменений поведения**.
Механически: `ReportsWorkspace` при `overview` рендерит существующий компонент дашборда. Если
`DashboardWorkspace` сам был `<main>`-корнем — тело извлекается в под-компонент, обёртка снимается
(паттерн «воркспейс как сегмент» из Касса-слияния: `embedded`-тело без внешнего `<main>`).

### 4.3. «История» — 4 отчёта (feature-shape `reports/history/`)

Один параметризованный слой на 4 отчёта (DRY — не 4 копии):

- **`reports/history/HistoryDestination.tsx`** — под-таб-полоса на 4 отчёта (вложенность
  рейл→destination→таб уже принята в Кассе/Складе). Активный таб → соответствующий отчёт.
- **`reports/history/reportTypes.ts`** — декларативный реестр 4 отчётов: `{ id, labelKey,
  load(client, branchId, range), build(dto, fmt), exportCsv(client, branchId, range), csvName }`.
  Один источник правды по колонкам/загрузке/экспорту на тип.
- **`reports/history/useReport.ts`** — общий хук загрузки (loading/error/ready + retry),
  повторяет проверенную форму `useReport` из Platform.Web (cancel-on-unmount, tick-retry).
- **`reports/history/reportModel.ts`** — маппинг `ReportResultDto` (`Record<string, unknown>`)
  → `{ summaryCards, columns, rows }` на каждый из 4 типов. Структура колонок/summary берётся
  **1:1 с Platform.Web `club/reports/reportsModel.ts`** (там уже решено, что показывать):
  - **Смены:** state / opened / closed / movements / expectedCash / countedCash / difference.
  - **Кассовые операции:** source / opType / impact / reason / created + summary (cashIn/cashOut/net).
  - **Игровое время:** seat / device / playerKind / state / duration / revenue + summary
    (duration/package/bonus/revenue).
  - **Действия операторов:** operator / action / outcome / count / first / last + summary (totalActions).
- **`reports/history/ReportTable.tsx`** — рендер `ReportView` в атомах оператора (`table-panel` /
  `ctable-head` / `ctable-row` + `EmptyState` + `Skeleton`), **не** shadcn (Platform.Web-версия на
  shadcn `Table`/`Card` — переписываем под kit оператора). Кнопка CSV-экспорт (паттерн скачивания
  как в Касса→Смена). Диапазон дат — переиспользуем `dateRange.ts` из №2 (пресеты + кастом).

Деньги: `MoneyDto`-поле — `minorUnits` (как в dashboard №2); формат через `formatMinorUnits` /
`<Money>` из `operatorPrimitives`/`currencyFormat.ts`. Минуты из секунд — `Math.round(sec/60)`.

### 4.4. «Журнал» — аудит филиала (feature-shape `reports/journal/`)

Переиспользование UI из №2 (Сеть→Журнал), но **branch-scoped**:

- **`reports/journal/BranchJournalDestination.tsx`** — тонкий аналог `network/journal/JournalDestination`,
  но зовёт `audit.search(branchId, query)` (branch endpoint) вместо org-версии.
- Переиспользуем как есть: `network/journal/OrgAuditFilters.tsx`, `dateRange.ts`, форму строки-аудита
  (`orgAuditModel.ts` → строки + `outcomeChipTone`). Если компоненты №2 жёстко завязаны на org-клиент —
  извлекаем общую презентационную часть (фильтры + модель строк) в переиспользуемый модуль, org- и
  branch-версии подают только разный `search`-вызов. **Без дублирования** UI.
- Право видимости — `audit.view` (branch-level, менеджерское). Endpoint per-branch — утечки нет.

### 4.5. Удаление мёртвого `logs`

Осиротевший недостижимый воркспейс удаляется полностью (закрывает записанный follow-up-долг):

- `BackendLogsWorkspace.tsx` (+ его тесты, если есть) — удалить.
- `WorkspaceId 'logs'` из `operatorTypes.ts`; `'logs'` из `workspaceIds` и `workspacePermissionRules`
  в `operatorPermissions.ts`; ветка `workspace === 'logs'` из `WorkspaceRouter.tsx`.
- Его функции поглощены: audit-search → Журнал; operator-actions/shift → История; диагностика уже
  на Карте (`diagnostics.getDiagnostics`). Проверить, что после удаления нет висячих импортов/типов
  (сборка `tsc -b` поймает).

---

## 5. Аудит паритета дублей (верификация, не код)

Прежде чем №4 удалит Platform.Web/club, доказать «оператор ≥ Platform.Web» поэкранно. Делегируется
агентам (sonnet), выход — отчёт со списком расхождений. Экраны для сверки:

- **Clients** (Platform.Web `/club/clients`) vs оператор «Клиенты».
- **Monetization** (тарифы/каталог/пакеты) vs Управление→Тарифы+Пакеты и Товары.
- **Settings** (профиль филиала + персонал) vs Управление→Клуб и Сотрудники.
- **Venue** (устройства/зоны) vs Карта + Управление→Залы и ПК (без floor-map-редактора/approval —
  осознанный descope, не дыра).
- **Overview/Reports/Journal** — после реализации разделов выше.

Классификация находок: **мелкая** (напр. отсутствующее поле формы) → чиню в №3; **крупная** → явно
фиксирую, выношу решение (в №3 или отложить в №4). Итог — «сертификат покрытия» в конце ветки.

---

## 6. i18n

Новые ключи под `op.reports.*` (switcher-ярлыки, вкладки Истории, колонки/summary отчётов, лимит-нота)
и `op.reports.journal.*` (или переиспользование существующих `op.network.journal.*`-строк, где
подходят по смыслу — колонки date/actor/action/target/outcome/source/details универсальны).
Все три локали (ru/en/tg) с реальным таджикским (гвард `messages.test.ts`: идентичные наборы ключей +
`tg !== ru` кроме allowlist-заимствований). Регенерация `packages/i18n/src/messages.ts` через `bun run gen`.

---

## 7. Тестирование

- **`reportsNav.test.ts`** — видимость destination по правам (reports.view → overview+history;
  audit.view → journal; оба → все три; ничего → секция скрыта).
- **`reportModel.test.ts`** — маппинг `ReportResultDto` → `ReportView` на все 4 типа (колонки, summary,
  деньги `minorUnits`, минуты, null-поля → «—»).
- **`HistoryDestination.test.tsx`** — переключение вкладок грузит нужный отчёт; пустой → `EmptyState`;
  CSV-кнопка зовёт нужный `export*` client.
- **`BranchJournalDestination.test.tsx`** — зовёт branch `audit.search` (не org), рендерит строку.
- **`operatorVisibility.test.ts`** — обновить: секция «Отчёты» доступна при reports.view ИЛИ audit.view;
  удаление `'logs'` из ожидаемого набора workspace.
- Гейты слайса: `bun test` (фронт) + `bun run build` (`tsc -b && vite` — тайпчекает и тесты) + i18n
  guard зелёные. Бэкенд не меняется — прогон бэкенд-тестов не требуется (но `bun run build` обязателен).

---

## 8. Риски

- **Перенос `DashboardWorkspace`:** если тело было `<main>`-корнем — снять обёртку аккуратно, иначе
  ремаунт/двойной `<main>`. Митигируется паттерном `embedded`-тела (уже применён в Кассе).
- **Удаление `logs`:** висячие импорты/типы. Митигируется `tsc -b` (тайпчек всей сборки + тестов).
- **Переиспользование журнала №2:** если org-компоненты завязаны на org-клиент — аккуратно извлечь
  презентационную часть, не сломав Сеть→Журнал (её тест `JournalDestination.test.tsx` должен остаться
  зелёным).
- **Аудит паритета** может вскрыть неожиданные крупные дыры → тогда пауза и решение с пользователем,
  не молчаливое расширение diff.

---

## 9. Итог

Фронтовая консолидация без изменений бэкенда: «Отчёты» → switcher (Обзор/История/Журнал), «История» =
4 отчёта (сверка кассы + два недостижимых), «Журнал» = аудит филиала (переиспользование №2), удаление
мёртвого `logs`, плюс сертификация паритета дублей для безопасного сноса Platform.Web/club в №4.
