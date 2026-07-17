# Operator «Управление» — редизайн начинки разделов в CRUD (список + drawer)

**Дата:** 2026-07-17
**Контекст:** Слайс 2 «Управления» обернул старые codex-секции (`SettingsLayoutSection` и др.) в новый каркас `ManagementScreen`. На QA-гейте пользователь отклонил результат: секции — это вечно-развёрнутые мега-формы с «раскиданными полями» и «кашей из кнопок» (по 3 кнопки Создать/Обновить/Удалить сверху и снизу). Обернуть плохую форму в хорошую рамку — всё ещё плохая форма. Это **класс проблемы** на всех тяжёлых разделах: Залы, Тарифы, Сотрудники, Товары.

**Решение (утверждено пользователем):** переделать НАЧИНКУ разделов в общепринятый CRUD-паттерн **список/таблица + боковой drawer**, тот же, что уже зашипан в «Клиентах». Раскатка: сначала «Залы и ПК» эталоном → показать → потом тем же паттерном Тарифы/Сотрудники/Товары.

## Что остаётся из слайса 2 (не выбрасываем)
Каркас `ManagementScreen` (заголовок/подзаголовок/ширина/loading-error-ready), левый рейл `ManagementWorkspace`, маршрутизация 8 разделов, права/видимость, подъём загрузки данных (`loadSettings`, слайсы `zones/staff/catalog/tariffs/packages/deviceInventory/...`), тосты. Меняется ТОЛЬКО тело каждого settings-раздела: вместо мега-формы — список+drawer.

## Что меняется
- «Клуб» — простая форма (4 поля), это честный одиночный кейс. Фикс: убрать пилюлю «Настройки загружены», прижать save-бар. Список/drawer НЕ нужен.
- «Залы и ПК», «Тарифы и пакеты», «Сотрудники и роли», «Товары» — CRUD список+drawer.
- «Оплата», «Лояльность», «Новости» — уже приемлемы, в этот редизайн не входят (правки по факту).

## Установленная инфраструктура (переиспользуем, не изобретаем)
Из разведки кодовой базы (`players/`, `operatorPrimitives.tsx`, `PanelModal.tsx`, `styles/12-players.css`):
- Паттерн таблица+drawer **инлайновый** в «Клиентах» (`ClientsTable`, `ClientDrawer`, `ClientActionsMenu`), НЕ примитив. CSS: `.table-panel`, `.ctable-grid/.ctable-head/.ctable-body/.ctable-row`, `.drawer-panel`, `.clients-grid` (грид «таблица + drawer 372px», при закрытом drawer таблица во всю ширину). Классы переиспользуемы (модификаторы строки держать под `.ctable-row`).
- Формы create/edit в «Клиентах» идут в **`PanelModal`** (портал, Esc/backdrop-close), не в drawer. Drawer = детали/контекст записи.
- Удаление — **`CriticalActionConfirmation`** (`operatorPrimitives`).
- ⋯-меню строки — **`ClientActionsMenu`** (a11y-dropdown: Esc/клик-вне/roving-focus). Паттерн копируем в generic.
- Каталог примитивов `operatorPrimitives.tsx`: `EmptyState`, `Skeleton`, `Money`, `CriticalActionConfirmation`, `StateFlag`, `FeedbackNotice`. Модалка отдельно — `PanelModal`.

## CRUD-кит (эталонная основа, `management/kit/`)
Чтобы не дублировать инлайн-паттерн 4 раза — тонкий переиспользуемый слой поверх готовых CSS/примитивов:

- **`MgmtTable<T>`** — панель-список: тулбар (заголовок + опц. поиск + первичная кнопка «+ Добавить»), заголовки колонок, строки (render-props), skeleton, empty-state. Строка кликабельна (`<button>`) → `onSelectRow`; опц. ⋯-меню на строке. Переиспользует CSS `.table-panel/.ctable-*`.
  ```ts
  interface MgmtColumn<T> { key: string; header: string; align?: 'start'|'end'; render: (row: T) => ReactNode; }
  interface MgmtTableProps<T> {
    columns: MgmtColumn<T>[];
    rows: T[]; rowKey: (row: T) => string;
    gridTemplate: string;                 // grid-template-columns
    selectedKey?: string | null; onSelectRow?: (row: T) => void;
    rowActions?: (row: T) => RowAction[]; // ⋯-меню
    toolbar?: { title?: string; search?: { value: string; onChange: (v: string) => void; placeholder: string };
                primary?: { label: string; icon?: ReactNode; onClick: () => void; disabled?: boolean } };
    isLoading?: boolean;
    empty: { icon?: ReactNode; title: string; description?: string; action?: { label: string; onClick: () => void } };
  }
  ```
- **`MgmtDrawer`** — правый `<aside>` (переиспользует `.drawer-panel`): head (title/subtitle + опц. ⋯-меню + закрыть), body (скролл), опц. footer. Управляемый (рендерится по условию у родителя), закрытие `onClose`.
  ```ts
  interface MgmtDrawerProps { title: string; subtitle?: string; actions?: RowAction[]; onClose: () => void; children: ReactNode; footer?: ReactNode; }
  ```
- **`RowActionsMenu`** — generic ⋯-dropdown (адаптация `ClientActionsMenu`): `{ actions: RowAction[] }`, `RowAction = { id; label; icon?; onClick; danger?; disabled? }`. Полная a11y (Esc/клик-вне/стрелки/возврат фокуса).
- **`MgmtMasterDetail`** (опц. обёртка) — грид «таблица + drawer» (по образцу `.clients-grid`): при закрытом drawer таблица во всю ширину.
- Формы create/edit → **`PanelModal`** + маленькая форма-грид (лейбл сверху инпута; кнопка сабмита primary; отмена = крестик). Удаление → **`CriticalActionConfirmation`**.

Тарифы/Сотрудники/Товары = один `MgmtTable` + `MgmtDrawer`/`PanelModal`. Копируются почти без дизайна.

## «Залы и ПК» — эталон (две вкладки внутри раздела)
Два домена (планировка / устройства) нельзя валить в одну простыню — раздел даёт intra-screen вкладки (как Касса; это НЕ третий уровень рейла).

### Вкладка «Залы и места»
- `MgmtTable` залов: колонки **Зал · Кол-во ПК · Порядок · ⋯**. Тулбар: «+ Зал».
- Клик по залу → `MgmtDrawer`: поля зала (Название, Порядок; сохранить) + вложенный `MgmtTable`/список ПК этого зала (**ПК · Порядок · ⋯**), кнопка «+ ПК». Правка ПК → строка/мини-форма в дровере; удаление ПК/зала → ⋯ + `CriticalActionConfirmation`.
- Создание зала/ПК → `PanelModal`.
- Операции A1–A6 (createZone/updateZone/deleteZone/createSeat/updateSeat/deleteSeat) — все сохранены.

### Вкладка «Устройства»
- `MgmtTable` устройств: колонки **Устройство · Статус (online/locked) · Зал·Место · Здоровье · ⋯**. Тулбар: «+ Подключить устройство».
- «+ Подключить устройство» → `PanelModal`: срок кода + кнопка «Создать код» → показать код подключения (B1 createEnrollmentCode).
- Клик по устройству → `MgmtDrawer`: карточка (статус/heartbeat/agent/shell) + назначение на место (select места → назначить, B2) + команды (тип lock/unlock + причина → отправить, B4) + ключи (выдать новый → показать секрет, B6; отозвать → confirm, B7) + история команд устройства. Открытие карточки (B3) — по клику (drawer сам грузит detail). Обновить историю филиала (B5) — действие в тулбаре/дровере.
- Security-группа B (enrollment/assign/command/rotate/revoke) — двойной permission-гейт (проп в UI + серверный `hasPermission`) сохранить; revoke за `CriticalActionConfirmation`.

Все 13 операций, 8 permission-гейтов, 3 источника данных + 3 колбэка обновления — переносятся 1:1, меняется только подача (список+drawer+modal вместо мега-форм).

## Acceptance
- Ни одного вечно-развёрнутого мега-формата; на экране — список, форма открывается по требованию (drawer/modal).
- Одна первичная кнопка «+ Добавить» на список; действия записи — в ⋯-меню/дровере, не враскидку.
- Нет пилюли «Настройки загружены».
- Деньги нейтральные; филиал по имени не UUID; loading=skeleton, error=конкретный текст+retry, empty=честный.
- Функционально: все операции старых секций работают (особенно устройства/ключи).
- `bun test` + `bun run build` + i18n parity зелёные.
