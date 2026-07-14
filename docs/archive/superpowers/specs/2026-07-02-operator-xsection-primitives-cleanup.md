# Operator UI-kit — кросс-секционная уборка общих примитивов

**Статус:** одобрено (вместо паритета Карты — реальная архитектурная польза).
**Родительский эпик:** [`2026-07-01-operator-ui-kit-design.md`](2026-07-01-operator-ui-kit-design.md).
Предыдущие: S1 Клиенты, S2 Склад, S3 Касса (в main).

## 1. Проблема (из разведки Карты)

Два ОБЩИХ (кросс-секционных) примитива стилизуются CSS-ом из `06-map-grid.css` (файл Карты), хотя
используются по всему приложению — архитектурный смелл (тронешь Карту → едут 6 разделов):

- **`StateFlag`** (`operatorPrimitives.tsx`) рендерит `.state-flag` (CSS в `06-map-grid.css:447–504`).
  Потребители: Клиенты, Логи, Касса, Review, Склад, Брони (счётчики в шапках).
- **`CriticalActionConfirmation`** рендерит обёртку `.critical-confirmation-actions` (CSS в
  `06-map-grid.css:828–1220`). Потребители: Карта, Касса, Склад, Настройки×2, PaymentDialog.

Плюс **латентный баг из S3**: `.state-flag span` (10px tertiary, для метки) — descendant-селектор,
ловит вложенный `<span class="ui-money">` внутри `<strong>{value}</strong>` (Касса шапка передаёт
`<Money>` как value) → значения «В кассе/Выручка» подсажены в 10px/tertiary. Тесты (jsdom) не видят.

## 2. Решение

**Карту как раздел НЕ мигрируем** (seat-grid штучный, ROI плохой). Делаем только уборку:

### 2.1 `StateFlag → .ui-chip--count` (миграция общего примитива)
`.ui-chip--count` уже зарезервирован в `02-ui-kit.css` как замена StateFlag, но неполон. Достраиваем
его до ВИЗУАЛЬНОГО ПАРИТЕТА с `.state-flag`, затем `StateFlag`-компонент рендерит `.ui-chip ui-chip--count`,
затем удаляем `.state-flag` CSS из `06-map-grid.css`.

Достройка `.ui-chip--count` (в `02-ui-kit.css`) до паритета с `.state-flag` (значения сверить дословно
при реализации; где токен == сырое значение — токен, иначе сырое с пометкой):
- контейнер: pill, height 26px, border 1px default, bg surface-elevated, padding как у `.state-flag` (9px);
- **метка `.ui-chip--count > span`** (ПРЯМОЙ ребёнок!) — tertiary, малый размер (как `.state-flag span`
  10px). Прямой ребёнок КРИТИЧЕН: чинит money-span баг (вложенный `.ui-money` в `<strong>` не попадает
  под правило метки);
- значение `.ui-chip--count > strong` — text-primary (как `.state-flag strong` 11px);
- точки: `.ui-chip--count.is-critical::before` (красная, из `.state-flag.critical::before`),
  `.ui-chip--count.is-warning::before` (жёлтая, уже есть).

`StateFlag`-компонент (`operatorPrimitives.tsx`): маппинг `critical` → класс `is-critical`,
`tone` (напр. `warning`) → `is-{tone}`; рендер `<span class="ui-chip ui-chip--count …">` вместо
`.state-flag`. Внутренняя разметка `<span>{label}</span><strong>{value}</strong>` сохраняется.
(Проверить фактические значения `tone`, что передаёт `warning`; критичность — булев `critical`.)

*Приёмка:* во ВСЕХ 6 потребителях счётчики выглядят 1:1 как раньше; **деньги в шапке Кассы вернулись к
нормальному размеру/цвету** (money-span баг устранён); критичные/warning точки на месте.

### 2.2 Перенос CSS `.critical-confirmation-actions` (чистая релокация)
Правила `.critical-confirmation-actions*` (`06-map-grid.css:828–1220`) — ДОСЛОВНО перенести в общий
файл примитивов `16-workspace-primitives.css` (где живёт логика примитивов), сохранив правила байт-в-байт
и относительный порядок каскада. Это pure move — вид не меняется. Кнопки на `.ui-btn` в этом слайсе НЕ
мигрируем (риск; отдельно при желании).

*Приёмка:* во ВСЕХ 6 потребителях (Карта/Касса/Склад/Настройки×2/PaymentDialog) диалоги подтверждения
выглядят 1:1; `06-map-grid.css` больше не содержит `.critical-confirmation-actions`.

## 3. Что НЕ трогаем

- Раздел «Карта» (seat-tile/timebar/side-panel/фильтры) — не мигрируем (штучный, ROI плохой).
- Прочие map-специфичные классы в `06-map-grid.css` (`.seat-*`, `.detail-*`, `.map-*`) — остаются.
- Кнопки внутри `.critical-confirmation-actions` — не на `.ui-btn` (только релокация CSS).

## 4. Гейт и риски

- **Визуальный паритет во ВСЕХ разделах-потребителях** — главный критерий (StateFlag: Клиенты/Логи/
  Касса/Review/Склад/Брони; confirmation: Карта/Касса/Склад/Настройки/PaymentDialog). Проверяется live-превью.
- **Тесты:** `bun test` (вся сюита) + `App.test` + `bun run build` зелёные. Тесты, ассертящие на
  `.state-flag`/`.critical-confirmation-actions` (если есть) — перевести на новые селекторы, не ослабляя.
- **Каскад при релокации:** `06-map-grid.css` грузится ПОСЛЕ `16-workspace-primitives.css`? Проверить
  порядок `@import` — при переносе `.critical-confirmation-actions` в 16 убедиться, что специфичность/
  порядок не меняют победителя каскада (иначе диалоги поедут).
- **`.ui-chip--count` паритет:** сверить каждое свойство с `.state-flag` дословно; расхождение = регресс
  в 6 разделах. Money-span фикс проверить на шапке Кассы.
- **Не сломать Карту:** `StateFlag`/`.critical-confirmation-actions` используются и в Карте — удаление их
  CSS из map-файла должно совпасть с новым домом 1:1.
