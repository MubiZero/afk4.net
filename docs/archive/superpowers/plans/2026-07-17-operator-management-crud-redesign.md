# План: CRUD-редизайн разделов «Управления» (список + drawer)

Спека: `docs/superpowers/specs/2026-07-17-operator-management-crud-redesign.md`. Ветка: `feat/operator-management-redesign` (продолжаем).

## Глобальные ограничения
- Переиспользовать существующую инфраструктуру: CSS `.table-panel/.ctable-*/.drawer-panel` (`styles/12-players.css`), `PanelModal`, `CriticalActionConfirmation`, `EmptyState`, `Skeleton`, `Money`, `StateFlag`. Не изобретать своё поверх принятого.
- Все операции старых секций сохранить 1:1 (особенно устройства: enrollment/assign/command/rotate/revoke). Двойной permission-гейт (UI-проп + серверный `hasPermission`).
- Деньги нейтральные (не amber). Филиал по имени, не UUID. i18n через `@afk4/i18n`, реальный tg, регенерация `messages.ts` через `bun run gen`.
- Тосты через существующий feedback-канал. Состояния loading/error/empty через `ManagementScreen`.
- Гейт на слайс: `cd src/AFK4.Operator.App.Web && bun test && bun run build` + `cd packages/i18n && bun test` — всё зелёное.
- Визуальные детали (spacing/hierarchy/состояния кнопок/фокус) — interface-limb, на токенах surface-elevation (light=подъём).

## СЛАЙС A — CRUD-кит (`management/kit/`)
- **A1. `RowActionsMenu`** — generic ⋯-dropdown (адаптация `players/ClientActionsMenu`): `actions: RowAction[]`, полная a11y. Тест: рендер пунктов, danger-стиль, Esc/клик-вне закрывают, onClick вызывается.
- **A2. `MgmtTable<T>`** — панель-список (тулбар+заголовки+строки+skeleton+empty), переиспользует `.table-panel/.ctable-*`. Тест: рендер строк/колонок, клик по строке → onSelectRow, primary-кнопка, empty при 0 строк, skeleton при isLoading, ⋯-меню если rowActions.
- **A3. `MgmtDrawer`** — правый `<aside>` (`.drawer-panel`): head(title/subtitle/⋯/close)+body(scroll)+footer. Тест: рендер title/children, close→onClose, actions→⋯-меню.
- **A4. CSS-раскладка** — грид «таблица+drawer» для management (по образцу `.clients-grid`, свои классы `mgmt-*` или переиспользование). Скелет/пустое консистентны с ManagementScreen. interface-limb.

## СЛАЙС B — «Залы и ПК» эталон (две вкладки)
- **B1. Каркас вкладок** в `HallsDevicesDestination`: intra-screen tabs «Залы и места» / «Устройства» (idiom как Касса). i18n-ключи вкладок.
- **B2. Вкладка «Залы и места»**: `MgmtTable` залов (Зал·Кол-во ПК·Порядок·⋯) + `MgmtDrawer` зала (поля зала + вложенный список ПК + «+ПК»/правка/удаление). Создание зала/ПК → `PanelModal`. Удаление → confirm. Операции A1–A6. Тест: список залов, drawer, +Зал/+ПК модалка, удаление confirm.
- **B3. Вкладка «Устройства»**: `MgmtTable` устройств + «+ Подключить» (модалка enrollment) + `MgmtDrawer` устройства (карточка/назначение/команды/ключи/история). Операции B1–B7, security-гейты. Тест: список, drawer, назначение, команда, rotate показывает секрет, revoke confirm, enrollment-модалка.
- **B4. Снять старый `SettingsLayoutSection`** из маршрута halls (заменён B2/B3). Пока НЕ удалять файл (удаление в слайсе D — вдруг ещё где-то). Проверить, что halls рендерит новое.
- **B5. QA-гейт эталона**: превью «Залы и ПК» dark+light, показать пользователю. Ждём ОК на паттерн ДО раскатки.

## СЛАЙС C — Раскатка паттерна
- **C1. «Тарифы и пакеты»**: `MgmtTable` тарифов + `MgmtTable` пакетов (или один список с типом) + drawer/modal редактирования. Цена нейтральная. Операции создания/версий тарифа, пакетов.
- **C2. «Сотрудники и роли»**: `MgmtTable` сотрудников + drawer/modal (приглашение/профиль/роль/enable-disable/reset-pin). Опасные — confirm.
- **C3. «Товары»**: `MgmtTable` каталога + drawer/modal (категория/товар/делистинг) + штрихкоды. Движение склада уже убрано.
- **C4. «Клуб» полировка**: убрать пилюлю «Настройки загружены», прижать save-бар (форма остаётся формой).

## СЛАЙС D — Уборка + PR
- **D1.** Удалить старые секции, ставшие мёртвыми (`SettingsLayoutSection`, `SettingsTariffsSection`, `SettingsStaffSection`, `SettingsGoodsSection` — если полностью заменены), `BackendSettingsWorkspace`, `SettingsIntegrationsSection`. Вычистить осиротевший CSS/i18n (`op.settings.stock.*` и пр.) через grep-гейт + regen.
- **D2.** Разрулить skip-долг в `App.test.tsx` (POS/движение-склада/логи скипы).
- **D3.** Бэклог (События отдельным рейлом, паблишинг обновлений → owner web).
- **D4.** Полный гейт + PR + auto-merge после зелёного CI.
