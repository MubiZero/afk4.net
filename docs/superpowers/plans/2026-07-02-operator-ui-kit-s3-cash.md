# Operator UI-kit — S3 «Касса» Implementation Plan (паритет через реконсиляцию атома)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Перевести вёрстку раздела «Касса» (POS/смена/журнал/модалки) на общий UI-kit `.ui-*` +
`<Money>`, сохранив визуальный вид (Касса — нравящийся эталон), убрав дубли `pos-*`/`cash-*`.

**Architecture:** Атомы `.ui-*` были извлечены из Кассы, но местами причёсаны → реконсиляция: где
эталон = «как надо», подтягиваем атом (правильный модификатор / один новый аддитивный вариант
`.ui-card--elevated`), где расхождение микроскопично — принимаем значение атома, где структура
специфична — тонкий cash-слой. Миграция покомпонентная (заменяем класс на `.ui-*`), осиротевший CSS +
каскад-ловушки чистятся финальной задачей. Кросс-секционное (`cash-tab`, `StateFlag`,
`.critical-confirmation-actions`, `.pos-orders-ticker`/`.pos-order-*`, `.pos-embed`) — НЕ трогаем.

**Tech Stack:** React + TSX, plain CSS (barrel `src/styles.css`), Vite (`tsc -b && vite`),
`bun test` (happy-dom + testing-library + jest-dom), i18n `@afk4/i18n`.

## Global Constraints

- **Только атомы** `.ui-*` + `<Money>`. Единственная правка общего `02-ui-kit.css` — ADDITIVE:
  новый `.ui-card--elevated` (не меняет существующие правила → Клиенты/Склад целы).
- **Деньги-path FROZEN:** меняем только рендер денег; `cashModel.ts`/`shiftReport.ts` НЕ трогаем.
- **Извлечение денег:** `formatMoney(value, currencyCode)` принимает объект `{minorUnits, currencyCode}`
  или `null`. `<Money>` принимает число → на границе: `<Money minorUnits={value?.minorUnits ?? 0} currencyCode={currencyCode} />`.
  Сайты с `formatMinorUnits(n, currencyCode)` — уже число, передаём напрямую. Импорт
  `import { Money } from '../operatorPrimitives';` (для `src/BackendPosWorkspace.tsx` — `./operatorPrimitives`).
- **Знак денег:** ТОЛЬКО лента кассовых операций (`CashOperationsLedger`, переезжает на ledger-грамматику)
  → `<Money … signed />`. Все прочие деньги (POS-тоталы/цены, герои смены, ряды, чеки, отчёт, шапка,
  мини-лента движений смены) → обычный `<Money>` (паритет — без принудительного «+»).
- **i18n:** только существующие ключи `op.cash.*`/`op.pos.*`; хардкод-строк не добавляем.
- **Каскад/приём:** в TSX старый визуальный класс ЗАМЕНЯЕМ на `.ui-*`; осиротевшие правила и
  дочерние тег-селекторы (`.X button`/`.X input`, перебивающие атомы) — удаляет Task 5.
- **НЕ мигрируем / КЛАССЫ СОХРАНИТЬ:** `.cash-tab*` (общий со Складом), `StateFlag` компонент и
  `.state-flag` CSS (общий с Картой), `.critical-confirmation-actions` (общая с модалками карты),
  `.pos-orders-ticker` + весь `.pos-order-*` субдерево, `.pos-embed`. Тесты/`App.test` ассертят на
  `.pos-orders-ticker`/`.pos-embed`/`.cash-shift-exports`/`.cash-export-error` — эти классы сохранить.
- **Гейт слайса** (Task 5): `bun test` (вся сюита) + отдельный `App.test` + `bun run build` зелёные.
- **Визуальный паритет** — главный критерий; проверяется live-превью Кассы до мержа. После атом-правки
  (`.ui-card--elevated`) — регресс-глянец Клиентов/Склада (ожидаемо без изменений — аддитивно).

## Migration Vocabulary (единый словарь)

- Кнопки: `pos-primary-action`→`.ui-btn ui-btn--primary ui-btn--lg`; `pos-secondary-action`→базовый
  `.ui-btn`; `cash-command-btn`→`.ui-btn ui-btn--ghost ui-btn--sm` (danger-вариант +`ui-btn--danger`);
  `cash-primary-action`→`.ui-btn ui-btn--primary ui-btn--lg ui-btn--block` (danger +`ui-btn--danger`).
- Чипы: `pos-category-row > button`→`.ui-chip ui-chip--filter` (+`.is-active` вместо `.active`).
- Карточки: `pos-product-card`→`.ui-card ui-card--interactive`; `cash-shift-card`→`.ui-card ui-card--elevated`.
- Поля: `pos-search`/`cash-shift-form` inputs → `.ui-field` (обёртка `<label>`) + `.ui-field > input`.
- Деньги: `<Money>` (см. Global Constraints).
- Лента: `cash-ledger-row`→грамматика `.ui-ledger-*`.

---

### Task 1: POS (BackendPosWorkspace)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`

**Interfaces:**
- Consumes: `<Money>` (`./operatorPrimitives`), атомы. `currencyCode: string` — уже проп.

- [ ] **Step 1: Категории → `.ui-chip--filter`**

Контейнер `.pos-category-row` (строка ~383) ОСТАВИТЬ (структурный). Кнопку категории (стр. 385–392):
```tsx
className={`ui-chip ui-chip--filter${activeCategory === category ? ' is-active' : ''}`}
```
(было `className={activeCategory === category ? 'active' : undefined}`).

- [ ] **Step 2: Карточка товара → `.ui-card--interactive` + деньги**

Строка ~403: `className="pos-product-card"` → `className="ui-card ui-card--interactive"`. Внутри цена
(стр. ~406): `{formatMinorUnits(product.priceMinorUnits, currencyCode)}` →
`<Money minorUnits={product.priceMinorUnits} currencyCode={currencyCode} />` (оставить обёртку `<b>`).

- [ ] **Step 3: POS-поиск → `.ui-field`**

`<label className="pos-search">` (стр. ~375) → `<label className="ui-field">`; `<Search/>` иконку
оставить; `<input>` без своего класса (стилизуется `.ui-field > input`).

- [ ] **Step 4: Кнопки чека + тотал**

Тотал (стр. ~532): `{formatMinorUnits(cartTotalMinorUnits, currencyCode)}` →
`<Money minorUnits={cartTotalMinorUnits} currencyCode={currencyCode} />` (обёртка `<strong>` оставить).
Кнопки (стр. 534–535):
- `className="pos-primary-action"` → `className="ui-btn ui-btn--primary ui-btn--lg"`.
- `className="pos-secondary-action"` → `className="ui-btn"`.

- [ ] **Step 5: Прочие денежные сайты POS**

Заменить оставшиеся `formatMinorUnits(...)` касательного UI (напр. стр. ~522) на `<Money>`. НЕ трогать
`.pos-orders-ticker`/`.pos-order-*` (лента заказов — сохранить как есть).

- [ ] **Step 6: Прогнать тест POS**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/BackendPosWorkspace.test.tsx`
Expected: PASS (тест ассертит `.pos-embed` — сохранён; остальное по роли/тексту).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx
git commit -m "feat(operator-cash): POS на .ui-* — категории/карточка/поиск/кнопки/деньги"
```

---

### Task 2: Смена (CashShiftWorkspace + CashShiftHeader + CashShiftCommandBar) + атом `.ui-card--elevated`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css` (добавить `.ui-card--elevated`)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx`
- Test: соответствующие `*.test.tsx`

**Interfaces:**
- Produces: `.ui-card--elevated` (общий атом — поднятая карточка с тенью) для этой и будущих задач.
- Consumes: `<Money>`, атомы, `currencyCode`.

- [ ] **Step 1: Добавить аддитивный атом `.ui-card--elevated`**

В `src/styles/02-ui-kit.css` после `.ui-card--stat`-блока (после строки 190) добавить:
```css
.ui-card--elevated { background: var(--surface-elevated); box-shadow: var(--shadow-card); }
```
Обновить reserved-комментарий (строка 139), убрав `.ui-card--elevated` из зарезервированных, если там есть.

- [ ] **Step 2: Карточки смены → `.ui-card--elevated`**

В `CashShiftWorkspace.tsx` три `<section className="cash-shift-card …">` (стр. ~116, 129, 142):
`cash-shift-card` → `ui-card ui-card--elevated` (модификатор `cash-shift-card--lead` оставить, если несёт
раскладку). `.cash-shift-hero*`/`.cash-shift-row`/`.cash-shift-movements`/`.cash-shift-hist-*` — ОСТАВИТЬ
(тон/раскладка).

- [ ] **Step 3: Деньги смены → `<Money>` (обычный, тон сохраняют классы-обёртки)**

Заменить все `formatMoney(value, currencyCode)` в `CashShiftWorkspace.tsx` на
`<Money minorUnits={value?.minorUnits ?? 0} currencyCode={currencyCode} />`, сохранив обёртки
(`<strong className="cash-shift-hero">`, `<strong>` в `.cash-shift-row`, `<b>` в movements/hist) и их
тон-классы (`attention`/`ok`/`muted`/`.out`/`.in`). Сайты: стр. ~118, 120–124, 133, 136–138, 155, 187, 189.
(Напр. `<strong className="cash-shift-hero"><Money minorUnits={current.earned.total.minorUnits} currencyCode={currencyCode} /></strong>`.)

- [ ] **Step 4: Деньги шапки → `<Money>` в StateFlag (StateFlag НЕ трогать)**

В `CashShiftHeader.tsx` (стр. 69–70) `value={formatMoney(header.cashInHand, currencyCode)}` →
`value={<Money minorUnits={header.cashInHand?.minorUnits ?? 0} currencyCode={currencyCode} />}` (и revenue).
`StateFlag` компонент/класс не трогаем (`value` уже принимает ReactNode после S2).

- [ ] **Step 5: Командные кнопки → `.ui-btn`**

В `CashShiftCommandBar.tsx` (стр. ~148–168): `className="cash-command-btn"` →
`className="ui-btn ui-btn--ghost ui-btn--sm"`; `className="cash-command-btn danger"` →
`className="ui-btn ui-btn--ghost ui-btn--sm ui-btn--danger"`. Иконки/`aria-hidden` оставить.

- [ ] **Step 6: Прогнать тесты смены**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftWorkspace.test.tsx src/cash/CashShiftHeader.test.tsx src/cash/CashShiftCommandBar.test.tsx`
Expected: PASS (ассерты по роли/тексту; `.cash-export-error` сохранён).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css \
        src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx \
        src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx
git commit -m "feat(operator-cash): Смена — .ui-card--elevated (новый атом) + деньги <Money> + командные кнопки .ui-btn"
```

---

### Task 3: Журнал/леджеры (CashOperationsLedger + CashReceiptsLedger)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx`
- Test: `src/cash/CashOperationsLedger.test.tsx`, `src/cash/CashReceiptsLedger.test.tsx`

**Interfaces:**
- Consumes: `<Money>`/`<Money signed>`, `.ui-ledger-*` грамматика, `currencyCode`.

- [ ] **Step 1: Лента операций → `.ui-ledger-*` (миграция #8) + `<Money signed>`**

В `CashOperationsLedger.tsx` контейнер-`<ul>` списка добавить класс `ui-ledger-list` (класс панели
`.cash-ledger-list`, если он на `<ul>`, оставить рядом — структурный). Строку (стр. ~119–124) заменить:
```tsx
<li key={readString(row, 'operationId')} className="ui-ledger-row">
  <span className="ui-ledger-time">{formatTime(readString(row, 'createdAtUtc'))}</span>
  <div className="ui-ledger-body">
    <span className="ui-ledger-title">{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</span>
    <span className="ui-ledger-detail">{readString(row, 'reason')}</span>
  </div>
  <span className="ui-ledger-aside">
    <Money minorUnits={impact?.minorUnits ?? 0} currencyCode={currencyCode} signed />
  </span>
</li>
```
(`const impact = readMoney(row, 'cashImpact');` уже есть выше в map. Класс `out`/`in` на `<li>` убрать —
тон теперь несёт `<Money signed>`.)

- [ ] **Step 2: Чеки → структура остаётся, деньги → `<Money>`**

В `CashReceiptsLedger.tsx` `.pos-receipt-row` (стр. ~189) ОСТАВИТЬ (фикс-колонки). Денежное число
(стр. ~196 и прочие `formatMoney`): `<b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>` →
`<b><Money minorUnits={readMoney(row, 'total')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>`.
Заменить остальные `formatMoney` в файле (стр. ~172, 175, 208–209, 212, 219, 242) аналогично (обычный `<Money>`).

- [ ] **Step 3: Прогнать тесты леджеров**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashOperationsLedger.test.tsx src/cash/CashReceiptsLedger.test.tsx src/cash/CashJournalWorkspace.test.tsx`
Expected: PASS (`.cash-export-error` сохранён; ассерты по тексту/роли).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx \
        src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx
git commit -m "feat(operator-cash): лента операций на .ui-ledger-* + <Money signed>, чеки — деньги на <Money>"
```

---

### Task 4: Модалки (Open/Close/Movement/Report)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashMovementModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.tsx`
- Test: соответствующие `*.test.tsx`

**Interfaces:**
- Consumes: `<Money>`, `.ui-field`, `.ui-btn`, `currencyCode`.

Примечание: форма `.cash-shift-form` (обёртка) ОСТАЁТСЯ (макет); поля внутри — на `.ui-field`. Кнопки —
не внутри `.critical-confirmation-actions` (у Кассы submit прямо в форме) → мигрируем на `.ui-btn`.

- [ ] **Step 1: OpenShiftModal — поля + submit**

Каждую пару `<label htmlFor=…>…</label><input …/>` (стр. ~42–47) обернуть в `.ui-field`:
```tsx
<label className="ui-field">
  <span>{t('op.cash.open.startingCashLabel')}</span>
  <input id="open-shift-cash" inputMode="decimal" value={startingCash} disabled={busy} onChange={…} />
</label>
```
(структура «метка-текст в `<span>` + input» = `.ui-field`; `htmlFor`/`id` можно убрать, метка теперь
оборачивает input). Submit (стр. ~49): `className="cash-primary-action"` →
`className="ui-btn ui-btn--primary ui-btn--lg ui-btn--block"` (иконку оставить).

- [ ] **Step 2: CashMovementModal — поля + submit**

Аналогично Step 1: поля amount/reason → `.ui-field`; кнопка `cash-primary-action` →
`ui-btn ui-btn--primary ui-btn--lg ui-btn--block`.

- [ ] **Step 3: CloseShiftModal — поля + submit(danger) + деньги preview**

Поля counted/note → `.ui-field`. Блок `.cash-close-reconcile` ОСТАВИТЬ (раскладка); деньги внутри
(`formatMoney(expectedCash, …)`, `formatMoney(difference, …)`) → `<Money minorUnits={…?.minorUnits ?? 0} currencyCode={currencyCode} />`
(тон-класс `attention` на обёртке оставить). Submit `className="cash-primary-action danger"` →
`className="ui-btn ui-btn--primary ui-btn--lg ui-btn--block ui-btn--danger"`.

- [ ] **Step 4: ShiftReportModal — деньги helper + print-кнопка**

Helper `money()` (стр. 25–26) сейчас возвращает строку через `formatMoney`. Заменить его тело на возврат
JSX `<Money>`:
```tsx
const money = (value: { currencyCode: string; minorUnits: number } | null) =>
  value === null ? t('op.cash.shift.notClosed') : <Money minorUnits={value.minorUnits} currencyCode={currencyCode} />;
```
(вызовы `{money(...)}` в `<strong>` остаются). Print-кнопка `className="cash-primary-action"` →
`className="ui-btn ui-btn--primary ui-btn--lg ui-btn--block"`. `.cash-report*`/`.cash-shift-row` — оставить.

- [ ] **Step 5: Прогнать тесты модалок**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/OpenShiftModal.test.tsx src/cash/CloseShiftModal.test.tsx src/cash/CashMovementModal.test.tsx src/cash/ShiftReportModal.test.tsx`
Expected: PASS (ассерты по роли `button`/тексту метки — устойчивы к `.ui-field`).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.tsx \
        src/AFK4.Operator.App.Web/src/cash/CashMovementModal.tsx \
        src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.tsx \
        src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.tsx
git commit -m "feat(operator-cash): модалки — поля на .ui-field, submit на .ui-btn--primary, деньги <Money>"
```

---

### Task 5: Чистка `11-pos.css` + `21-cash.css` + токен-нормализация + гейт + регресс-глянец

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/11-pos.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:**
- Consumes: результат Tasks 1–4 (в TSX больше нет ссылок на осиротевшие классы).

- [ ] **Step 1: Удалить осиротевшие правила (grep-verify 0 ссылок в TSX)**

Метод (для каждого — сначала `grep -rn "\bКЛАСС\b" src` подтверждает 0 ссылок в TSX, включая
template-литералы/условия):
- `11-pos.css`: `.pos-primary-action`, `.pos-secondary-action`, `.pos-product-card` (+дочерние
  `strong/span/b/em`), `.pos-search` (+`.pos-search input`), `.pos-category-row button` (+`.active`).
- `21-cash.css`: `.cash-command-btn` (+`.danger`), `.cash-primary-action` (+`.danger`), `.cash-shift-card`.
- **НЕ удалять (сохранить):** `.pos-orders-ticker`/`.pos-order-*`, `.pos-embed`, `.pos-receipt-row`(+колонки),
  `.pos-category-row` (контейнер), `.cash-tab*`, `.cash-shift-form` (форма), `.cash-shift-hero*`,
  `.cash-shift-row`, `.cash-shift-movements`, `.cash-shift-hist-*`, `.cash-ledger*` (панель), `.cash-close-*`,
  `.cash-report*`, `.cash-head*`.

- [ ] **Step 2: Снести каскад-ловушки (дочерние тег-селекторы, перебивающие атомы)**

Удалить дочерние правила, которые тег-селектором (0,1,1) перебивают атомы `.ui-*` (0,1,0) на
мигрированных элементах: `.pos-category-row button` (все состояния — кнопки теперь `.ui-chip--filter`),
`.pos-search input`, `.pos-product-card strong/span/b/em`, `.cash-shift-form input`, а также селекторы
внутри бывшей `.cash-ledger-row` (`.cash-ledger-row span/strong/em/b`) — строки теперь `.ui-ledger-*`.
Контейнеры (`.pos-category-row`, `.cash-shift-form`, `.cash-ledger*`-панель) оставить.

- [ ] **Step 3: Нормализация токенов + чистка редундантности**

- В сохранённых правилах сырые `border-radius: 6px/7px` → `var(--radius-sm)`; прочие сырые
  gap/padding/font-size px в тронутых блоках → `--space-*`/`--text-*` (грид-ширины колонок — оставить).
- `.cash-shift-hero` — убрать дублирующие `font-family: var(--font-mono)` + `tabular-nums` (их несёт
  вложенный `<Money>`/`.ui-money`), оставить размер (`--text-xl`) и тон.

- [ ] **Step 4: Гейт слайса**

```bash
cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun test src/App.test.tsx && /home/fedya/.bun/bin/bun run build
```
Expected: вся сюита + App.test зелёные; `bun run build` (`tsc -b && vite`) без ошибок.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/11-pos.css src/AFK4.Operator.App.Web/src/styles/21-cash.css
git commit -m "chore(operator-cash): чистка осиротевшего CSS/каскад-ловушек + нормализация на токены (гейт слайса)"
```

---

## Self-Review

**Spec coverage (`2026-07-02-operator-cash-ui-kit-design.md`):**
- §2 таблица дивергенций: #1 GO→`--lg` (T1), #2 secondary→база (T1), #3 command-btn→ghost--sm (T2),
  #4 primary-action→primary--lg--block (T4), #5 shift-card→`.ui-card--elevated` (T2 +атом), #6 hover 1px
  (принято, не трогаем атом), #7 input padding (принято через `.ui-field`), #8 ledger→`.ui-ledger-*` (T3),
  #9 receipt тонкий слой (T3), #10 StateFlag отложен (не трогаем). ✓
- §3 деньги→`<Money>`: T1 (POS), T2 (смена+шапка), T3 (леджеры), T4 (модалки/отчёт). ✓
- §4 атом `.ui-card--elevated` (аддитивный): T2 Step 1. ✓
- §5 кросс-секционное не трогаем: Global Constraints + T5 «не удалять». ✓
- §6 гейт/паритет/регресс-глянец: T5 + финальный whole-branch. ✓

**Placeholder scan:** нет TBD; все шаги с конкретной разметкой/командами. Условные («если класс несёт
раскладку») — детерминированные проверки на месте.

**Type consistency:** `<Money minorUnits={number} currencyCode={string} signed?={bool}>` единообразно;
объект→число через `?.minorUnits ?? 0` везде; `.ui-card--elevated` вводится в T2, ledger-грамматика в T3.
