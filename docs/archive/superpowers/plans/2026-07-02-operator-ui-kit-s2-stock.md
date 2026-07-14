# Operator UI-kit — S2 «Склад» Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Перевести вёрстку раздела «Склад» operator-приложения на уже готовый общий UI-kit
(атомы `.ui-*` + `<Money>`) и починить 7 UX-огрехов раскладки, не трогая денежную логику.

**Architecture:** Раздел = 4 вкладки (Остатки · Приёмка · Журнал · Инвентаризация) + диалог
списания + шапка, оркестрируются `StockWorkspace.tsx`. Атомы (`02-ui-kit.css`) и `<Money>`
(`operatorPrimitives.tsx`) уже существуют (S0/S1) — нового общего кода почти не пишем.
Миграция покомпонентная: в TSX **заменяем** секционный класс на `.ui-*` (не добавляем — иначе
старое правило `22-stock.css` выигрывает каскад, т.к. грузится после `02-ui-kit.css`), тесты на
удаляемых классах переводим на устойчивые селекторы. Финальная задача одним проходом удаляет
осиротевший CSS и нормализует уцелевшие правила на токены.

**Tech Stack:** React + TSX, plain CSS (barrel `src/styles.css`), Vite (`tsc -b && vite`),
тесты `bun test` (happy-dom + @testing-library/react + jest-dom), i18n `@afk4/i18n`.

## Global Constraints

- **Только атомы** `.ui-*` из `02-ui-kit.css` + компонент `<Money>`; новых секционных `stock-*`
  дублей не плодим.
- **Деньги-path FROZEN:** меняем только рендер денег (на `<Money>`); логику расчётов в
  `stockLevels.ts`/`receivingModel.ts`/`journalModel.ts`/`inventoryModel.ts` НЕ трогаем.
- **i18n:** используем только существующие ключи `op.stock.*`; хардкод-строк не добавляем.
- **Каскад/приём миграции:** в TSX старый визуальный класс ЗАМЕНЯЕМ на `.ui-*` (не добавляем
  рядом). Структурные классы (грид-раскладка колонок, контейнеры-списки) сохраняем.
- **НЕ мигрируем** (общее с эталоном, кросс-секционный слайс): `StockTabBar` (`cash-tab*`,
  общий с Кассой) и счётчики `StateFlag` в `StockHeader` (общие с Картой/Кассой). Трогаем в
  шапке только денежное значение.
- **Плотные таблицы** (Остатки/Приёмка/Инвентаризация) остаются табличными строками —
  мигрируем их СОДЕРЖИМОЕ (чипы/деньги/кнопки/поля), строки в `.ui-card` не превращаем. На
  грамматику `.ui-ledger-*` перекраивается ТОЛЬКО Журнал (событийный лог).
- **Токены (§8 спеки):** в изменяемых правилах — никаких сырых px для отступов/радиусов/шрифтов;
  исключения (обоснованная геометрия): грид-ширины колонок, степпер, прогресс-бар.
- **Тон движения журнала:** `purchase`→`is-live`, `sale`→`is-neutral`, `refund`→`is-booking`,
  `adjustment` при `quantityDelta ≥ 0`→`is-warning`, при `quantityDelta < 0`→`is-danger`.
- **Гейт слайса** (финальная задача): `bun test` (вся сюита) + отдельный прогон `App.test` +
  `bun run build` (`tsc -b && vite`) — всё зелёное. `tsc -b` тайпчекает и тесты.

## Migration Vocabulary (единый словарь для всех задач)

Атомы уже существуют — вот их применение:

- **Кнопки** `.ui-btn` + `--primary` (заливка), `--danger`, `--ghost`, `--sm`, `--block`.
- **Чипы** `.ui-chip` + `--filter` (тумблер, active-класс `.is-active`); `--status` + тон
  `.is-live`/`.is-booking`/`.is-warning`/`.is-danger`/`.is-neutral`; `--xs`.
- **Карточка-метрика** `.ui-card ui-card--stat` (+ `is-danger`), точная разметка (из
  `ClientDetail.tsx:140`):
  ```tsx
  <div className="ui-card ui-card--stat">
    <span>{t('…label…')}</span>
    <strong><Money minorUnits={n} currencyCode={currencyCode} /></strong>
  </div>
  ```
  `.ui-card--stat > span` = мелкая метка (`--text-xs`, `--text-secondary`); `> strong` = крупное
  значение (`--text-lg`, mono через вложенный `<Money>`). `is-danger` — только когда метрика
  сигналит проблему (напр. `outCount > 0`).
- **Поле** `.ui-field` (обёртка `<label>` или `<div>`) + `.ui-field > input`:
  ```tsx
  <label className="ui-field">
    <span>{t('…label…')}</span>
    <input … />
  </label>
  ```
- **Деньги** `<Money minorUnits={n} currencyCode={currencyCode} />`; знаковые (движения/разницы)
  — `<Money … signed />` (сам добавляет `+`/`−` и цвет `--pos`/`--neg`). Пустое/ноль →
  `<span className="ui-money ui-money--muted">—</span>` (модификатор добавляется в Task 1).
- **Фильтры** — ряд `.ui-chip--filter` с `.is-active` вместо `.seg`/`.period` кнопок.

---

### Task 1: Вкладка «Остатки» (StockLevelsWorkspace)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css` (добавить 1 модификатор `.ui-money--muted`)
- Test: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx`

**Interfaces:**
- Consumes: `<Money>` из `../operatorPrimitives`; атомы `.ui-*`. `currencyCode: string` — уже проп.
- Produces: `.ui-money--muted` (общий модификатор приглушённых денег) для последующих задач.

- [ ] **Step 1: Добавить модификатор приглушённых денег в атом-слой**

В `src/styles/02-ui-kit.css` сразу после строки `.ui-money--neg { color: var(--danger-text); }`
(строка 230) добавить:
```css
.ui-money--muted { color: var(--text-quaternary); }
```

- [ ] **Step 2: Обновить тест статуса остатка на `.ui-chip`**

В `StockLevelsWorkspace.test.tsx` заменить проверки строк 29 и 31:
```tsx
// было: container.querySelectorAll('.stock-status-tag.low') / '.stock-status-tag.out'
expect(container.querySelectorAll('.ui-chip--status.is-warning').length).toBeGreaterThan(0);
expect(container.querySelectorAll('.ui-chip--status.is-danger').length).toBeGreaterThan(0);
```
Селектор `.cash-stock-list` (строка 45) НЕ трогаем — контейнер-список остаётся структурным.

- [ ] **Step 3: Прогнать тест — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: FAIL (нет элементов `.ui-chip--status.is-warning/.is-danger`).

- [ ] **Step 4: Мигрировать фильтры на `.ui-chip--filter`**

В `StockLevelsWorkspace.tsx` контейнер `.seg` (стр. 124) заменить на ряд чипов; каждую кнопку
фильтра (стр. 125–148) — с `className={filter === 'all' ? 'on' : ''}` на:
```tsx
className={`ui-chip ui-chip--filter${filter === 'all' ? ' is-active' : ''}`}
```
(аналогично для `'low'`/`'out'`). Контейнер `.seg` → `className="stock-filters"` (структурный
flex-ряд; правило добавит Task 6 при нормализации, либо переиспользовать существующий flex —
оставить `.seg` на контейнере допустимо, мигрируются только кнопки). Оставляем `.seg` на
контейнере, меняем ТОЛЬКО кнопки.

- [ ] **Step 5: Мигрировать поле поиска на `.ui-field`**

Обёртку `.panel-search` (стр. 150) оставить (позиционирование справа), input (стр. 151–157)
обернуть логикой `.ui-field`: добавить классы — контейнер `className="ui-field panel-search"`,
input без своего класса (стилизуется через `.ui-field > input`). Если у поиска нет видимой
метки — оставить только input внутри `.ui-field` (метка не обязательна для search).

- [ ] **Step 6: Починить статус остатка (UX-1) — убрать текст-тег, оставить кант + чип**

В строке товара (стр. 188–210):
- Статус-тег (стр. 207–210) заменить на статус-чип:
  ```tsx
  {status !== 'ok' && (
    <span className={`ui-chip ui-chip--status ${status === 'low' ? 'is-warning' : 'is-danger'}`}>
      {t(status === 'low' ? 'op.stock.status.low' : 'op.stock.status.out')}
    </span>
  )}
  ```
- Иконку-якорь (стр. 189–191, `Boxes`/`AlertTriangle`) ОСТАВИТЬ как есть (scan-anchor).
- Кант строки: класс строки `.cash-stock-row srow ${status}` оставить — кант (`border-left` в
  тон) нормализуется в Task 6 (сейчас `box-shadow inset 3px`; станет token'd `border-left`).

- [ ] **Step 7: Мигрировать деньги строки на `<Money>`**

- Себестоимость (стр. 213–215) и цена (стр. 217–219):
  ```tsx
  <div className="money">
    {item.avgCostMinorUnits > 0
      ? <Money minorUnits={item.avgCostMinorUnits} currencyCode={currencyCode} />
      : <span className="ui-money ui-money--muted">—</span>}
  </div>
  ```
  (для цены — `item.priceMinorUnits`). Класс-обёртку `.money` оставить временно (удалит Task 6).
- Стоимость позиции (стр. 221–223):
  ```tsx
  <div className="valm">
    {stockVal > 0
      ? <Money minorUnits={stockVal} currencyCode={currencyCode} />
      : <span className="ui-money ui-money--muted">—</span>}
  </div>
  ```

- [ ] **Step 8: Мигрировать иконочные кнопки строки на `.ui-btn`**

- Кнопка «＋» (приёмка, стр. 226–233): `className="iact"` → `className="ui-btn ui-btn--sm ui-btn--ghost"`.
- Кнопка «−» (списание, стр. 234–241): `className="iact minus"` → `className="ui-btn ui-btn--sm ui-btn--danger"`.
- Иконки `<Plus/>` / `<Minus/>` и `aria-label` внутри — оставить.

- [ ] **Step 9: Мигрировать сводку (UX-4) — стат-карточка + primary-кнопка**

Правую сводку (стр. 263–295):
- `<div className="ctx-card">` c заголовком `.ctx-title` и `<div className="ctx-big">…</div>`
  заменить на стат-карточку:
  ```tsx
  <div className="ui-card ui-card--stat">
    <span>{t('op.stock.summary.totalValue')}</span>
    <strong><Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} /></strong>
  </div>
  ```
- Счётчики «мало»/«нет» под ней оставить как есть по разметке (текст + число); критический
  счётчик «нет» тонировать danger только при `outCount > 0` (если это отдельная стат-карточка —
  добавить `is-danger` условно; если строки — оставить текущую подачу, тон правится в Task 6).
- Кнопку `.ctx-btn` (стр. 295) → `className="ui-btn ui-btn--primary ui-btn--block"`.

- [ ] **Step 10: Прогнать тесты вкладки — зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx \
        src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css
git commit -m "feat(operator-stock): Остатки на .ui-* — фильтры/поле/статус-чип/деньги/кнопки/стат-сводка"
```

---

### Task 2: Вкладка «Приёмка» (ReceivingWorkspace)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx`

**Interfaces:**
- Consumes: `<Money>`, атомы, `currencyCode`, `.ui-money--muted` (из Task 1).
- Produces: —.

Примечание: степпер количества (`.recv-step`) и поле себестоимости с суффиксом (`.recv-cost`) —
специализированные локальные контролы, в TSX **не меняются** (класс сохраняется); их токен-
нормализация и `:focus-visible` — в Task 6.

- [ ] **Step 1: Мигрировать поле поиска товара и бейдж сканера**

- Контейнер `.recv-add-field` (стр. 174) → обёртка `.ui-field`: `className="ui-field recv-add-field"`;
  input (стр. 175–180) без своего класса (через `.ui-field > input`).
- Бейдж сканера `.recv-scanner-badge` (стр. 184–187) → `className="ui-chip ui-chip--status is-live"`
  (активный сканер = «живой» индикатор). `aria-label` оставить.

- [ ] **Step 2: Мигрировать сумму строки и кнопку удаления**

- Сумма строки (стр. 252): `<div className="recv-sum">{formatMinorUnits(lineSubtotalMinorUnits(line), currencyCode)}</div>`
  → `<div className="recv-sum"><Money minorUnits={lineSubtotalMinorUnits(line)} currencyCode={currencyCode} /></div>`.
- Кнопка удаления `.recv-del` (стр. 253–255) → `className="ui-btn ui-btn--sm ui-btn--danger"`.

- [ ] **Step 3: Мигрировать поля накладной на `.ui-field`**

Поля поставщика (стр. 268–270) и номера накладной (стр. 272–274):
`<label className="recv-field">` → `<label className="ui-field">` (внутренняя структура
`<span>{t(...)}</span><input … />` уже совпадает с `.ui-field`).

- [ ] **Step 4: Мигрировать итог и кнопку проведения**

- Сумма документа (стр. 282): `<b>{formatMinorUnits(totals.sumMinorUnits, currencyCode)}</b>`
  → `<b><Money minorUnits={totals.sumMinorUnits} currencyCode={currencyCode} /></b>` (обёртку
  `.mv recv-grand` оставить).
- Кнопка «Провести приёмку» `.ctx-btn` (стр. 283–286) → `className="ui-btn ui-btn--primary ui-btn--block"`
  (иконку `<Check/>` и логику `posting` оставить).
- Итоговую сводку приёмки (позиции/штуки/сумма), если это `.ctx-card` — по образцу Task 1 Step 9
  привести денежную метрику к `.ui-card ui-card--stat`; счётчики позиций/штук оставить.

- [ ] **Step 5: Прогнать тест вкладки**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ReceivingWorkspace.test.tsx`
Expected: PASS (тест на `aria-label`/role — устойчив; проверить, что рендер не сломан).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx
git commit -m "feat(operator-stock): Приёмка на .ui-* — поля/бейдж сканера/деньги/кнопки/стат-итог"
```

---

### Task 3: Вкладка «Журнал» (JournalWorkspace) + тон движения

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/journalModel.ts` (добавить `movementStatusTone`)
- Modify: `src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/journalModel.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.test.tsx`

**Interfaces:**
- Consumes: `<Money>`, `.ui-ledger-*` грамматика, `.ui-chip--status`, `currencyCode`.
  `type MovementType = 'purchase' | 'sale' | 'refund' | 'adjustment'` (`journalModel.ts:4`).
- Produces: `movementStatusTone(type: MovementType, quantityDelta: number): 'is-live' | 'is-neutral' | 'is-booking' | 'is-warning' | 'is-danger'`.

- [ ] **Step 1: Написать падающий unit-тест тона движения**

В `journalModel.test.ts` добавить:
```ts
import { movementStatusTone } from './journalModel';

describe('movementStatusTone', () => {
  it('приход/возврат/продажа — по типу', () => {
    expect(movementStatusTone('purchase', 5)).toBe('is-live');
    expect(movementStatusTone('sale', -3)).toBe('is-neutral');
    expect(movementStatusTone('refund', 2)).toBe('is-booking');
  });
  it('коррекция: излишек — warning, недостача/списание — danger', () => {
    expect(movementStatusTone('adjustment', 4)).toBe('is-warning');
    expect(movementStatusTone('adjustment', 0)).toBe('is-warning');
    expect(movementStatusTone('adjustment', -4)).toBe('is-danger');
  });
});
```

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/journalModel.test.ts`
Expected: FAIL (`movementStatusTone is not a function`).

- [ ] **Step 3: Реализовать `movementStatusTone`**

В `journalModel.ts` добавить экспорт:
```ts
export function movementStatusTone(
  type: MovementType,
  quantityDelta: number,
): 'is-live' | 'is-neutral' | 'is-booking' | 'is-warning' | 'is-danger' {
  switch (type) {
    case 'purchase':
      return 'is-live';
    case 'refund':
      return 'is-booking';
    case 'sale':
      return 'is-neutral';
    case 'adjustment':
      return quantityDelta < 0 ? 'is-danger' : 'is-warning';
  }
}
```

- [ ] **Step 4: Прогнать — зелёный**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/journalModel.test.ts`
Expected: PASS.

- [ ] **Step 5: Мигрировать фильтры типа и периода на `.ui-chip--filter`**

- Кнопки типа (стр. 149–157): `className={typeFilter === filter ? 'on' : ''}` →
  `className={`ui-chip ui-chip--filter${typeFilter === filter ? ' is-active' : ''}`}`
  (`aria-pressed` оставить). Контейнер `.seg` (стр. 147) оставить на контейнере.
- Кнопки периода (стр. 217–222): `className={period === value ? 'on' : ''}` →
  `className={`ui-chip ui-chip--filter${period === value ? ' is-active' : ''}`}`. Контейнер
  `.period` (стр. 215) оставить.

- [ ] **Step 6: Перекроить строку журнала на грамматику `.ui-ledger-*` (UX-5)**

Контейнер списка `<ul className="jlist">` → `<ul className="jlist ui-ledger-list">`. Каждую
строку `.jrow` (стр. 190–202) заменить на:
```tsx
<li key={row.id} className="ui-ledger-row">
  <span className="ui-ledger-time">{dateTimeFmt.format(new Date(row.createdAtUtc))}</span>
  <div className="ui-ledger-body">
    <span className="ui-ledger-title">
      <span className={`ui-chip ui-chip--status ${movementStatusTone(row.type, row.quantityDelta)}`}>
        {stockMovementTypeLabel(row.type, t)}
      </span>
      {row.name}
    </span>
    <span className="ui-ledger-detail">
      {row.sku}{row.reason ? ` · ${row.reason}` : ''}{row.who ? ` · ${row.who}` : ''}
    </span>
  </div>
  <span className="ui-ledger-aside">
    <span className="ui-money">{row.quantityDelta > 0 ? '+' : ''}{row.quantityDelta} {t('op.stock.journal.unit')}</span>
    {row.sumMinorUnits > 0
      ? <Money minorUnits={row.quantityDelta < 0 ? -row.sumMinorUnits : row.sumMinorUnits} currencyCode={currencyCode} signed />
      : <span className="ui-money ui-money--muted">—</span>}
  </span>
</li>
```
Импортировать `movementStatusTone` из `./journalModel`. Удалить локальное вычисление `tone`
(plus/warn/minus), если оно больше не используется.

- [ ] **Step 7: Мигрировать деньги итогов журнала на `<Money>`**

В сводке (стр. 232, 234) заменить `formatMinorUnits(summary.inboundSumMinor, currencyCode)` и
`formatMinorUnits(summary.writtenOffSumMinor, currencyCode)` на
`<Money minorUnits={summary.inboundSumMinor} currencyCode={currencyCode} />` и
`<Money minorUnits={summary.writtenOffSumMinor} currencyCode={currencyCode} />` соответственно
(обёртки `.totrow`/`.in`/`.wn` и знак «+»/«−» перед кол-вом оставить). Денежную метрику-итог,
если оформлена как `.ctx-card` — привести к `.ui-card ui-card--stat` (Task 1 Step 9).

- [ ] **Step 8: Обновить тест журнала (если ассертит на `.jrow`/`.jtype`) и прогнать**

Проверить `JournalWorkspace.test.tsx`: `getByLabelText('Движения склада')` (стр. 39) —
устойчив. Если есть ассерты на `.jrow`/`.jtype`/`.jsum` — перевести на `.ui-ledger-row` /
`.ui-chip--status` / текст. Прогнать:
Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/JournalWorkspace.test.tsx src/stock/journalModel.test.ts`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/journalModel.ts \
        src/AFK4.Operator.App.Web/src/stock/journalModel.test.ts \
        src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.test.tsx
git commit -m "feat(operator-stock): Журнал — строка на .ui-ledger-*, тип=status-чип по тону, деньги=<Money signed>"
```

---

### Task 4: Вкладка «Инвентаризация» (InventoryWorkspace)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx`

**Interfaces:**
- Consumes: `<Money>` (signed), атомы, `currencyCode`, `.ui-money--muted`.
- Produces: —.

Примечание: поле факта (`.inv-fact` c dashed-empty и `ref`-менеджментом) и прогресс-бар
(`.inv-prog`) — специализированные локальные контролы; в TSX не мигрируем на `.ui-field`, класс
сохраняем; их токен-нормализация — в Task 6.

- [ ] **Step 1: Мигрировать знаковую сумму расхождения на `<Money signed>`**

Сумма расхождения (стр. 224–225): убрать локальный `signedSum` (стр. 120), заменить рендер:
```tsx
<div className={`inv-sum ${diffClass}`}>
  {pending || diff === 0
    ? '—'
    : <Money minorUnits={sum} currencyCode={currencyCode} signed />}
</div>
```
Расхождение по количеству (`inv-diff`, стр. 221–222) оставить как есть (это не деньги —
знаковые единицы; цвета `.zero/.plus/.minus` нормализуются в Task 6).

- [ ] **Step 2: Мигрировать деньги сводки на `<Money>`**

- Недостача (стр. 246): `{formatMinorUnits(totals.shortageSumMinorUnits, currencyCode)}` →
  `<Money minorUnits={totals.shortageSumMinorUnits} currencyCode={currencyCode} />` (текст
  `-{units} {unit} · ` и класс `.warning-text` оставить).
- Излишек (стр. 247): аналогично `totals.surplusSumMinorUnits`.
- Итого (стр. 248): `{formatMinorUnits(totals.netSumMinorUnits, currencyCode)}` →
  `<Money minorUnits={totals.netSumMinorUnits} currencyCode={currencyCode} signed={totals.netSumMinorUnits !== 0} />`
  (условный класс `.warning-text`/`.inv-pos` на `<b>` оставить).
- Денежную метрику-итог, если это `.ctx-card` — привести к `.ui-card ui-card--stat` (Task 1 Step 9).

- [ ] **Step 3: Мигрировать кнопки проведения и сброса**

- «Провести инвентаризацию» `.ctx-btn` (стр. 249–251) → `className="ui-btn ui-btn--primary ui-btn--block"`.
- Кнопку сброса `.inv-reset` → `className="ui-btn ui-btn--sm ui-btn--ghost"`.

- [ ] **Step 4: Прогнать тест вкладки**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/InventoryWorkspace.test.tsx`
Expected: PASS (`getByLabelText(/Факт по полке/)` — устойчив; проверить рендер сумм).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx
git commit -m "feat(operator-stock): Инвентаризация — знаковые деньги=<Money signed>, кнопки на .ui-btn, стат-итог"
```

---

### Task 5: Диалог списания (WriteOffDialog) + деньги шапки (StockHeader)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockHeader.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.test.tsx` (если существует)

**Interfaces:**
- Consumes: `<Money>`, `.ui-field`, `.ui-btn`, `currencyCode`.
- Produces: —.

- [ ] **Step 1: Мигрировать поля и кнопку диалога списания**

В `WriteOffDialog.tsx`:
- Поле количества (стр. 65–67) и причины (стр. 69–71): `<label className="recv-field">` →
  `<label className="ui-field">`.
- Себестоимость-дисплей (стр. 74): `{formatMinorUnits(Math.max(item.avgCostMinorUnits, 0), currencyCode)}`
  → `<Money minorUnits={Math.max(item.avgCostMinorUnits, 0)} currencyCode={currencyCode} />`.
- Кнопка списания (стр. 79–80): `className="danger"` → `className="ui-btn ui-btn--danger"`
  (в обёртке `.critical-confirmation-actions` — оставить).

- [ ] **Step 2: Мигрировать денежное значение шапки на `<Money>`**

В `StockHeader.tsx` метрику стоимости (стр. 52): `{formatMinorUnits(summary.totalValueMinorUnits, currencyCode)}`
→ `<Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} />`. Счётчики
`StateFlag` (стр. 54, 57) НЕ трогать (общий примитив, кросс-секционный слайс).

- [ ] **Step 3: Прогнать тесты диалога/шапки**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/WriteOffDialog.test.tsx src/stock/StockWorkspace.test.tsx`
Expected: PASS (если файла `WriteOffDialog.test.tsx` нет — прогнать `StockWorkspace.test.tsx`).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.tsx \
        src/AFK4.Operator.App.Web/src/stock/StockHeader.tsx
git commit -m "feat(operator-stock): диалог списания на .ui-field/.ui-btn, деньги шапки на <Money>"
```

---

### Task 6: Чистка `22-stock.css` + токен-нормализация (UX-7) + гейт слайса

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css`

**Interfaces:**
- Consumes: результат Tasks 1–5 (в TSX больше нет ссылок на осиротевшие классы).
- Produces: чистый секционный CSS; зелёный гейт слайса.

- [ ] **Step 1: Найти осиротевшие селекторы (нулевые ссылки в TSX)**

Для каждого кандидата убедиться, что в `src/stock/**/*.tsx` не осталось ссылок, затем удалить
правило из `22-stock.css`. Кандидаты (после Tasks 1–5 должны быть без ссылок):
`.stock-status-tag` (+`.low`/`.out`), `.money` (+`.dim`), `.valm` (+`.dim`), `.iact` (+`.minus`),
`.ctx-card`, `.ctx-title`, `.ctx-big`, `.ctx-btn`, `.recv-sum`, `.recv-field`, `.recv-del`,
`.recv-scanner-badge`, `.jrow`, `.jtime`, `.jtype` (+тона), `.jqty`, `.jsum`, `.jname`, `.jwho`,
`.jcols` (если есть), `.inv-sum`, а также `.seg button`/`.period button` визуальные правила
(`.on`, hover) — кнопки теперь `.ui-chip--filter`.

Метод для каждого селектора:
```bash
cd src/AFK4.Operator.App.Web && grep -rn "stock-status-tag\|\bmoney\b\|\bvalm\b\|\biact\b\|ctx-card\|ctx-title\|ctx-big\|ctx-btn\|recv-sum\|recv-field\|recv-del\|recv-scanner-badge\|\bjrow\b\|\bjtype\b\|\bjsum\b\|\bjqty\b\|inv-sum" src/stock
```
Ссылок в TSX быть не должно; если какая-то есть — значит соответствующий Task не завершил
миграцию, вернуть на доработку, а не удалять правило.

- [ ] **Step 2: Удалить подтверждённо мёртвые правила**

Удалить из `22-stock.css` подтверждённые в Step 1 осиротевшие блоки (включая их модификаторы и
`prefers-reduced-motion`/`transition` подмножества, ссылающиеся только на удалённые классы, напр.
`.iact`/`.ctx-btn`/`.recv-del` в блоке анимаций стр. 923–945).

- [ ] **Step 3: Нормализовать уцелевшие правила на токены (UX-7)**

В сохранённых правилах заменить сырые px на токены:
- Кант статуса строки: `.cash-stock-row.low`/`.out` — `box-shadow: inset 3px 0 0 …` →
  `border-left: 3px solid var(--warning)` / `var(--danger)` (3px канта допустим как геометрия).
- `.recv-step button`/`input` (30px), `.recv-cost` (30px), `.inv-fact` (32px) — привести высоту к
  `var(--control-md)` (36px), где визуально не ломает; смежные скругления степпера
  `6px` → `var(--radius-sm)`. Добавить `.recv-step button:focus-visible`,
  `.inv-reset:focus-visible` → `box-shadow: var(--focus-ring)` (полный набор состояний, UX-6).
- `.inv-prog i` `border-radius: 5px` → `var(--radius-sm)`.
- `.seg button` `padding: 3px 11px`, `.seg` `gap: 4px`, кластер `font-size: 12px/14px` в
  уцелевших блоках → `var(--space-*)` / `var(--text-xs|sm)`.
- Прочие сырые gap/padding/radius/font-size в изменённых блоках → соответствующие токены.
  Грид-ширины колонок (`88px 66px …`, `24px minmax(140px,1fr) …`) — ОСТАВИТЬ (раскладочные).

- [ ] **Step 4: Прогнать полную сюиту + App.test + сборку (гейт слайса)**

```bash
cd src/AFK4.Operator.App.Web && bun test && bun test src/App.test.tsx && bun run build
```
Expected: все тесты зелёные; `bun run build` (`tsc -b && vite`) без ошибок.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/22-stock.css
git commit -m "chore(operator-stock): чистка осиротевшего CSS + нормализация уцелевших правил на токены (гейт слайса)"
```

---

## Self-Review

**Spec coverage:**
- §3.1 Деньги→`<Money>`: Task 1 (money/valm/ctx-big), Task 2 (recv-sum/итог), Task 3 (jsum/итоги),
  Task 4 (inv-sum/сводка), Task 5 (шапка/себест диалога). ✓
- §3.2 Кнопки→`.ui-btn`: Task 1 (iact/ctx-btn), Task 2 (recv-del/ctx-btn), Task 4 (ctx-btn/inv-reset),
  Task 5 (danger). ✓
- §3.3 Карточки: сводки→`.ui-card--stat` (Tasks 1–4); табличные строки сознательно оставлены
  структурными (задокументировано в Global Constraints — уточнение к §3.3). ✓
- §3.4 Поля→`.ui-field`: Task 1 (поиск), Task 2 (recv-add/recv-field), Task 5 (диалог). Спец-контролы
  (recv-step/recv-cost/inv-fact) оставлены локальными, нормализованы в Task 6 — согласно §5 спеки. ✓
- §3.5 Чипы: статус остатка (Task 1), сканер (Task 2), тип движения (Task 3). ✓
- §3.6 Фильтры→`.ui-chip--filter`: Task 1 (остатки), Task 3 (тип+период). ✓
- §4 Тон движения: Task 3 `movementStatusTone` + тест. ✓
- §5 Compound: степпер/строка журнала — Task 3 (ledger-грамматика), степпер локальный (Task 6). ✓
- §6 UX-1…7: UX-1 (Task 1), UX-2 (все), UX-3 (Task 1,3), UX-4 (Tasks 1–4 стат-карточки),
  UX-5 (Task 3), UX-6 (Task 1,2,4 + focus-visible в Task 6), UX-7 (Task 6). ✓
- §7 Тесты: Task 1 (status-tag→chip), Task 3 (jrow при необходимости). ✓
- §8 Гейт/namespace/каскад/i18n/FROZEN: Global Constraints + Task 6. ✓

**Placeholder scan:** нет TBD/«обработать позже»; все шаги с конкретной разметкой и командами.
Условные места («если это `.ctx-card`», «если файл теста существует») — детерминированные проверки
на месте, не плейсхолдеры.

**Type consistency:** `movementStatusTone(type, quantityDelta)` — сигнатура и union совпадают в
Task 3 (тест, реализация, использование в строке). `<Money minorUnits currencyCode signed?>` —
единообразно во всех задачах. `.ui-money--muted` вводится в Task 1, потребляется в Tasks 3/4.
