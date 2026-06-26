# Operator «Склад» S4 — Инвентаризация Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить вкладку «Инвентаризация» в раздел Склад: пересчёт остатков по факту (со сканером), авторасчёт расхождений, проведение `adjustment`-корректировок на разницу в Журнал. Закрыть epic-долг — POS-хардкод порога низкого остатка.

**Architecture:** Чистый фронт поверх готового бэка (как S2). Новая вкладка `'inventory'` в `StockWorkspace`. Чистая модель `inventoryModel.ts` (зеркало `receivingModel.ts`): строки пересчёта, расхождение, итоги, селектор корректировок. Воркспейс `InventoryWorkspace.tsx` грузит каталог, показывает все отслеживаемые товары строками (учётный остаток), оператор вводит факт вручную (сканер находит+подсвечивает+фокусирует строку, НЕ +1 — спека §2 ограничивает «+1» приёмкой/POS), проведение = N идемпотентных `createStockMovement(adjustment, факт−учёт, себест=avgCost, reason="Инвентаризация")`. Бэк `CreateStockMovementAsync` уже умеет `adjustment`+reason и НЕ пересчитывает avg-cost на adjustment; журнал уже рендерит коррекции по reason-тексту. Миграций / новых эндпоинтов / контрактов нет.

**Tech Stack:** React 18 + TS, Vite, `bun test` (happy-dom + jest-dom, НЕ vitest), `@afk4/tokens` CSS, `@afk4/i18n` (ICU; `locales/{ru,en,tg}.json` в корне → `cd packages/i18n && bun run gen`). Тулчейн: `BUN=/home/fedya/.bun/bin/bun`. Деньги — minor units на проводе.

## Global Constraints

- **Токены `@afk4/tokens`, тема dark.** Акцент — emerald `var(--accent)` (#2cc592), НЕ синий.
- **Деньги — белый моноширинный.** Янтарь (`--warning`/`warning-text`) только для предупреждений (недостача), НЕ для нейтральных сумм. Emerald — излишек/приход. Отрицательные суммы — через `formatMinorUnits` (ASCII-минус), знак расхождения единиц — ASCII `+`/`-`.
- **Per-product порог `ReorderThreshold`, НЕ хардкод.** `0` = без алертинга. (Спека §5/§7.)
- **Контраст таблиц:** числа — `text-primary` (яркие); заголовки колонок/SKU — минимум `text-tertiary` (не quaternary); границы — `border-default`.
- **Реальные классы оболочки:** `stock-layout` (2 колонки: лист + `stock-summary`-aside ВНУТРИ колонки 2 — 3-я колонка shell НЕ задействована), `cash-tabs`/`cash-tab`, `ctx-card`/`ctx-btn`/`ctx-title`/`mv`, `recv-*`, `cash-stock-row`.
- **i18n:** новые ключи — во ВСЕ три локали (`ru`/`en`/`tg`), затем `cd packages/i18n && bun run gen`. tg — реальный таджикский, НЕ копия ru (guard `tg===ru` упадёт). Используемые `t()`-ключи обязаны существовать (key-existence guard).
- **`bun run build` = `tsc -b && vite build`** тайпчекает И тест-файлы → bun-моки типизировать. Финал слайса обязан включать `bun run build`, тесты `packages/i18n`, бэк-тесты и Windows-джоб (WPF) — но в S4 бэк не меняется (sanity-прогон достаточно).
- **Никаких AI-подписей** нигде (коммиты/PR/код/комменты). Никаких секретов. Никаких фейк-данных ради зелёного.
- **Деньги-инвариант:** складские движения меняют только qty/себест, не баланс игрока — money-action политику не задевают (операторские, под `inventory.stock.manage`).

---

## File Structure

- **Create** `src/AFK4.Operator.App.Web/src/stock/inventoryModel.ts` — чистая модель: `CountLine`, парс факта, расхождение, итоги, селектор корректировок. Зеркало `receivingModel.ts`.
- **Create** `src/AFK4.Operator.App.Web/src/stock/inventoryModel.test.ts` — юнит-тесты модели.
- **Create** `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx` — экран вкладки. Зеркало `ReceivingWorkspace.tsx`.
- **Create** `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx` — smoke + проведение + частичный сбой + сканер.
- **Modify** `src/AFK4.Operator.App.Web/src/stock/stockModel.ts` — `StockTab` += `'inventory'`, в `STOCK_TAB_ORDER`, `STOCK_TAB_PERMISSIONS`.
- **Modify** `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx` — `TAB_LABELS.inventory` + блок монтирования.
- **Modify** `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx` — тест видимости/переключения вкладки.
- **Modify** `src/AFK4.Operator.App.Web/src/styles/22-stock.css` — стили инвентаризации (scanbar, count-таблица, прогресс).
- **Modify** `locales/ru.json`, `locales/en.json`, `locales/tg.json` — ключи `op.stock.tab.inventory` + `op.stock.inventory.*`.
- **Modify** `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx` — epic-уборка: per-product порог вместо `stockOnHand <= 2`.
- **Modify** `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx` (или создать, если нет) — тест per-product порога.

---

## Task 1: Чистая модель инвентаризации

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/inventoryModel.ts`
- Test: `src/AFK4.Operator.App.Web/src/stock/inventoryModel.test.ts`

**Interfaces:**
- Consumes: `PosProductDto` from `../operatorApiClients`; `readString`/`readNumber`/`readBoolean`/`readArray` from `../operatorHelpers`.
- Produces (для Task 3): `CountLine`, `parseCounted`, `lineCounted`, `lineDiff`, `lineDiffSumMinorUnits`, `mapCatalogToCountLines`, `setCounted`, `markFresh`, `resetCounts`, `markPosted`, `InventoryTotals`, `inventoryTotals`, `InventoryAdjustment`, `inventoryAdjustments`.

- [ ] **Step 1: Write the failing test** — `src/AFK4.Operator.App.Web/src/stock/inventoryModel.test.ts`

```ts
import { describe, it, expect } from 'bun:test';
import {
  parseCounted, lineCounted, lineDiff, lineDiffSumMinorUnits,
  mapCatalogToCountLines, setCounted, markFresh, resetCounts, markPosted,
  inventoryTotals, inventoryAdjustments, type CountLine,
} from './inventoryModel';

const line = (over: Partial<CountLine> = {}): CountLine => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', barcodes: ['111'],
  systemQty: 12, avgCostMinorUnits: 400, countedText: '', fresh: false, ...over,
});

const product = (over: Record<string, unknown> = {}) => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true,
  stockOnHand: 12, avgCostMinorUnits: 400, barcodes: ['111'], ...over,
}) as never;

describe('inventoryModel', () => {
  it('parseCounted: целое >=0; пусто/невалид/отрицат → null', () => {
    expect(parseCounted('0')).toBe(0);
    expect(parseCounted('28')).toBe(28);
    expect(parseCounted(' 7 ')).toBe(7);
    expect(parseCounted('')).toBeNull();
    expect(parseCounted('  ')).toBeNull();
    expect(parseCounted('5.5')).toBeNull();
    expect(parseCounted('-3')).toBeNull();
    expect(parseCounted('abc')).toBeNull();
  });

  it('lineDiff: факт−учёт; пока не пересчитано → null', () => {
    expect(lineDiff(line({ countedText: '' }))).toBeNull();
    expect(lineDiff(line({ countedText: '12' }))).toBe(0);
    expect(lineDiff(line({ countedText: '10' }))).toBe(-2);
    expect(lineDiff(line({ countedText: '15' }))).toBe(3);
    expect(lineDiff(line({ countedText: '0' }))).toBe(-12);
  });

  it('lineCounted достаёт распарсенный факт', () => {
    expect(lineCounted(line({ countedText: '9' }))).toBe(9);
    expect(lineCounted(line({ countedText: '' }))).toBeNull();
  });

  it('lineDiffSumMinorUnits: знаковая сумма по средней себест.; null/0-расхождение → 0', () => {
    expect(lineDiffSumMinorUnits(line({ countedText: '' }))).toBe(0);
    expect(lineDiffSumMinorUnits(line({ countedText: '12' }))).toBe(0);
    expect(lineDiffSumMinorUnits(line({ countedText: '10' }))).toBe(-800); // -2 * 400
    expect(lineDiffSumMinorUnits(line({ countedText: '14' }))).toBe(800);  // +2 * 400
    expect(lineDiffSumMinorUnits(line({ countedText: '10', avgCostMinorUnits: -5 }))).toBe(0); // себест clamp 0
  });

  it('mapCatalogToCountLines: только trackStock, учётный=stockOnHand, факт пуст', () => {
    const lines = mapCatalogToCountLines([
      product(),
      product({ productId: 'p2', name: 'Время', trackStock: false }),
    ]);
    expect(lines).toHaveLength(1);
    expect(lines[0]).toMatchObject({ productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', systemQty: 12, avgCostMinorUnits: 400, countedText: '', fresh: false });
    expect(lines[0].barcodes).toEqual(['111']);
  });

  it('setCounted кладёт факт для товара, помечает fresh, снимает fresh с прочих', () => {
    const lines = [line({ productId: 'a', fresh: true }), line({ productId: 'b' })];
    const next = setCounted(lines, 'b', '5');
    expect(next.find((l) => l.productId === 'b')).toMatchObject({ countedText: '5', fresh: true });
    expect(next.find((l) => l.productId === 'a')!.fresh).toBe(false);
  });

  it('markFresh подсвечивает строку без изменения факта', () => {
    const lines = [line({ productId: 'a', fresh: true }), line({ productId: 'b', countedText: '3' })];
    const next = markFresh(lines, 'b');
    expect(next.find((l) => l.productId === 'b')).toMatchObject({ fresh: true, countedText: '3' });
    expect(next.find((l) => l.productId === 'a')!.fresh).toBe(false);
  });

  it('resetCounts очищает факт и подсветку всех строк', () => {
    const lines = [line({ countedText: '5', fresh: true }), line({ productId: 'b', countedText: '9' })];
    const next = resetCounts(lines);
    expect(next.every((l) => l.countedText === '' && !l.fresh)).toBe(true);
  });

  it('markPosted: учётный := факт (расхождение становится 0 — анти-двойное-проведение при ретрае)', () => {
    const lines = [line({ productId: 'a', countedText: '10' }), line({ productId: 'b', countedText: '9' })];
    const next = markPosted(lines, 'a');
    const a = next.find((l) => l.productId === 'a')!;
    expect(a.systemQty).toBe(10);
    expect(lineDiff(a)).toBe(0);
    expect(next.find((l) => l.productId === 'b')!.systemQty).toBe(12); // прочие не тронуты
  });

  it('inventoryTotals: пересчитано/расхождения/недостача/излишек/итог', () => {
    const lines = [
      line({ productId: 'a', systemQty: 12, countedText: '12' }), // совпало
      line({ productId: 'b', systemQty: 30, avgCostMinorUnits: 200, countedText: '28' }), // -2, -400
      line({ productId: 'c', systemQty: 6, avgCostMinorUnits: 700, countedText: '7' }),    // +1, +700
      line({ productId: 'd', systemQty: 2, countedText: '' }),     // не пересчитано
    ];
    const t = inventoryTotals(lines);
    expect(t.trackedCount).toBe(4);
    expect(t.countedCount).toBe(3);
    expect(t.discrepancies).toBe(2);
    expect(t.shortageUnits).toBe(2);
    expect(t.shortageSumMinorUnits).toBe(400);
    expect(t.surplusUnits).toBe(1);
    expect(t.surplusSumMinorUnits).toBe(700);
    expect(t.netSumMinorUnits).toBe(300); // -400 + 700
  });

  it('inventoryAdjustments: только пересчитанные с расхождением != 0; знаковая дельта, себест из avg', () => {
    const lines = [
      line({ productId: 'a', systemQty: 12, countedText: '12' }), // 0 → пропуск
      line({ productId: 'b', systemQty: 30, avgCostMinorUnits: 200, countedText: '28' }), // -2
      line({ productId: 'c', systemQty: 6, avgCostMinorUnits: 700, countedText: '7' }),    // +1
      line({ productId: 'd', systemQty: 2, countedText: '' }),     // null → пропуск
    ];
    expect(inventoryAdjustments(lines)).toEqual([
      { productId: 'b', quantityDelta: -2, unitCostMinorUnits: 200 },
      { productId: 'c', quantityDelta: 1, unitCostMinorUnits: 700 },
    ]);
  });

  it('масштаб: учётный 0 + факт 0 → нет расхождения; юникод-имя проходит', () => {
    expect(lineDiff(line({ systemQty: 0, countedText: '0' }))).toBe(0);
    const lines = mapCatalogToCountLines([product({ name: 'Шоколад «Алёнка» 🍫', stockOnHand: 0, avgCostMinorUnits: 0 })]);
    expect(lines[0].name).toBe('Шоколад «Алёнка» 🍫');
    expect(inventoryAdjustments(setCounted(lines, 'p1', '0'))).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Run test, verify it fails** — `BUN=/home/fedya/.bun/bin/bun; cd src/AFK4.Operator.App.Web && $BUN test src/stock/inventoryModel.test.ts`. Expected: FAIL (module not found).

- [ ] **Step 3: Implement** — `src/AFK4.Operator.App.Web/src/stock/inventoryModel.ts`

```ts
import type { PosProductDto } from '../operatorApiClients';
import { readString, readNumber, readBoolean, readArray } from '../operatorHelpers';

// Строка пересчёта: один отслеживаемый товар. countedText — сырой ввод факта (пусто = не пересчитано),
// fresh — только что отсканировано (для подсветки/фокуса).
export interface CountLine {
  productId: string;
  name: string;
  sku: string;
  barcodes: string[];
  systemQty: number;          // учётный остаток на момент загрузки
  avgCostMinorUnits: number;  // средняя себестоимость — для суммы расхождения
  countedText: string;
  fresh: boolean;
}

const NON_NEGATIVE_INT = /^\d+$/;

// Строка факта → целое >= 0, либо null (пусто/невалид/отрицат).
export function parseCounted(text: string): number | null {
  const trimmed = text.trim();
  if (!NON_NEGATIVE_INT.test(trimmed)) return null;
  const value = Number.parseInt(trimmed, 10);
  return Number.isFinite(value) && value >= 0 ? value : null;
}

export function lineCounted(line: CountLine): number | null {
  return parseCounted(line.countedText);
}

// Расхождение факт − учёт; null пока не пересчитано.
export function lineDiff(line: CountLine): number | null {
  const counted = lineCounted(line);
  return counted === null ? null : counted - line.systemQty;
}

// Знаковая сумма расхождения по средней себестоимости; 0 если не пересчитано / нет расхождения.
export function lineDiffSumMinorUnits(line: CountLine): number {
  const diff = lineDiff(line);
  if (diff === null) return 0;
  return diff * Math.max(line.avgCostMinorUnits, 0);
}

export function mapCatalogToCountLines(catalog: PosProductDto[]): CountLine[] {
  return catalog
    .filter((product) => readBoolean(product, 'trackStock'))
    .map((product) => ({
      productId: readString(product, 'productId'),
      name: readString(product, 'name'),
      sku: readString(product, 'sku', ''),
      barcodes: readArray<string>(product, 'barcodes'),
      systemQty: readNumber(product, 'stockOnHand', 0),
      avgCostMinorUnits: readNumber(product, 'avgCostMinorUnits', 0),
      countedText: '',
      fresh: false,
    }));
}

function withFresh(lines: CountLine[], productId: string, patch: (line: CountLine) => CountLine): CountLine[] {
  return lines.map((line) =>
    line.productId === productId ? patch({ ...line, fresh: true })
      : line.fresh ? { ...line, fresh: false } : line
  );
}

// Записать факт для товара (помечает строку fresh, снимает fresh с прочих).
export function setCounted(lines: CountLine[], productId: string, countedText: string): CountLine[] {
  return withFresh(lines, productId, (line) => ({ ...line, countedText }));
}

// Подсветить только что отсканированную строку без изменения факта.
export function markFresh(lines: CountLine[], productId: string): CountLine[] {
  return withFresh(lines, productId, (line) => line);
}

// Сбросить весь пересчёт.
export function resetCounts(lines: CountLine[]): CountLine[] {
  return lines.map((line) => ({ ...line, countedText: '', fresh: false }));
}

// После успешного проведения строки: учётный := факт (расхождение → 0), чтобы ретрай при
// частичном сбое не провёл её повторно. Если факт невалиден — не трогаем.
export function markPosted(lines: CountLine[], productId: string): CountLine[] {
  return lines.map((line) => {
    if (line.productId !== productId) return line;
    const counted = lineCounted(line);
    return counted === null ? line : { ...line, systemQty: counted };
  });
}

export interface InventoryTotals {
  trackedCount: number;
  countedCount: number;
  discrepancies: number;
  shortageUnits: number;             // суммарная недостача в единицах (положительное)
  shortageSumMinorUnits: number;     // положительное
  surplusUnits: number;
  surplusSumMinorUnits: number;
  netSumMinorUnits: number;          // знаковый итог: излишек +, недостача −
}

export function inventoryTotals(lines: CountLine[]): InventoryTotals {
  let countedCount = 0, discrepancies = 0;
  let shortageUnits = 0, shortageSumMinorUnits = 0;
  let surplusUnits = 0, surplusSumMinorUnits = 0, netSumMinorUnits = 0;
  for (const line of lines) {
    const diff = lineDiff(line);
    if (diff === null) continue;
    countedCount += 1;
    if (diff === 0) continue;
    discrepancies += 1;
    const sum = lineDiffSumMinorUnits(line);
    netSumMinorUnits += sum;
    if (diff < 0) { shortageUnits += -diff; shortageSumMinorUnits += -sum; }
    else { surplusUnits += diff; surplusSumMinorUnits += sum; }
  }
  return {
    trackedCount: lines.length, countedCount, discrepancies,
    shortageUnits, shortageSumMinorUnits, surplusUnits, surplusSumMinorUnits, netSumMinorUnits,
  };
}

export interface InventoryAdjustment {
  productId: string;
  quantityDelta: number;        // знаковая, != 0
  unitCostMinorUnits: number;   // из средней себестоимости (>= 0)
}

// Корректировки к проведению: только пересчитанные строки с расхождением != 0.
export function inventoryAdjustments(lines: CountLine[]): InventoryAdjustment[] {
  const out: InventoryAdjustment[] = [];
  for (const line of lines) {
    const diff = lineDiff(line);
    if (diff === null || diff === 0) continue;
    out.push({ productId: line.productId, quantityDelta: diff, unitCostMinorUnits: Math.max(line.avgCostMinorUnits, 0) });
  }
  return out;
}
```

- [ ] **Step 4: Run test, verify pass** — `cd src/AFK4.Operator.App.Web && $BUN test src/stock/inventoryModel.test.ts`. Expected: PASS (все кейсы).

- [ ] **Step 5: Commit** — `git add src/AFK4.Operator.App.Web/src/stock/inventoryModel.ts src/AFK4.Operator.App.Web/src/stock/inventoryModel.test.ts && git commit -m "feat(operator): модель инвентаризации — пересчёт, расхождения, корректировки"`

---

## Task 2: i18n-ключи инвентаризации

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify (generated): `packages/i18n/src/messages.ts` (через `bun run gen`)

**Interfaces:**
- Produces (для Task 3/4): ключи `op.stock.tab.inventory`, `op.stock.inventory.*` в `MessageKey`.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`** — рядом с существующими `op.stock.*` (вставить в JSON, соблюдая запятые). Точные значения:

```
"op.stock.tab.inventory": "Инвентаризация",
"op.stock.inventory.noPermission": "Недостаточно прав для инвентаризации",
"op.stock.inventory.loading": "Загрузка…",
"op.stock.inventory.title": "Пересчёт остатков",
"op.stock.inventory.scanHint": "Сканируйте товар для пересчёта — строка подсветится, введите фактическое количество",
"op.stock.inventory.search": "Поиск товара",
"op.stock.inventory.empty": "Нет товаров с учётом остатка",
"op.stock.inventory.colItem": "Товар",
"op.stock.inventory.colSystem": "Учётный",
"op.stock.inventory.colFact": "Факт по полке",
"op.stock.inventory.colDiff": "Расхождение",
"op.stock.inventory.colSum": "Сумма",
"op.stock.inventory.notCounted": "не пересчитано",
"op.stock.inventory.reset": "Сбросить",
"op.stock.inventory.progressTitle": "Прогресс пересчёта",
"op.stock.inventory.counted": "Пересчитано",
"op.stock.inventory.totalTitle": "Итог инвентаризации",
"op.stock.inventory.discrepancies": "Расхождений",
"op.stock.inventory.shortage": "Недостача",
"op.stock.inventory.surplus": "Излишек",
"op.stock.inventory.netCost": "Итог по себест.",
"op.stock.inventory.post": "Провести инвентаризацию",
"op.stock.inventory.posting": "Проведение…",
"op.stock.inventory.posted": "Инвентаризация проведена: {count, plural, one{# корректировка} few{# корректировки} many{# корректировок} other{# корректировки}}",
"op.stock.inventory.partial": "Проведено {posted} из {total} — повторите для остальных",
"op.stock.inventory.willCreate": "{count, plural, =0{нет расхождений} one{создаст # корректировку} few{создаст # корректировки} many{создаст # корректировок} other{создаст # корректировки}}",
"op.stock.inventory.reasonBase": "Инвентаризация"
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`:**

```
"op.stock.tab.inventory": "Inventory",
"op.stock.inventory.noPermission": "You don't have permission for inventory",
"op.stock.inventory.loading": "Loading…",
"op.stock.inventory.title": "Stock recount",
"op.stock.inventory.scanHint": "Scan a product to recount — the row highlights, enter the actual quantity",
"op.stock.inventory.search": "Search product",
"op.stock.inventory.empty": "No stock-tracked products",
"op.stock.inventory.colItem": "Product",
"op.stock.inventory.colSystem": "On record",
"op.stock.inventory.colFact": "Actual on shelf",
"op.stock.inventory.colDiff": "Difference",
"op.stock.inventory.colSum": "Amount",
"op.stock.inventory.notCounted": "not counted",
"op.stock.inventory.reset": "Reset",
"op.stock.inventory.progressTitle": "Recount progress",
"op.stock.inventory.counted": "Counted",
"op.stock.inventory.totalTitle": "Inventory summary",
"op.stock.inventory.discrepancies": "Discrepancies",
"op.stock.inventory.shortage": "Shortage",
"op.stock.inventory.surplus": "Surplus",
"op.stock.inventory.netCost": "Net at cost",
"op.stock.inventory.post": "Post inventory",
"op.stock.inventory.posting": "Posting…",
"op.stock.inventory.posted": "Inventory posted: {count, plural, one{# adjustment} other{# adjustments}}",
"op.stock.inventory.partial": "Posted {posted} of {total} — retry for the rest",
"op.stock.inventory.willCreate": "{count, plural, =0{no discrepancies} one{will create # adjustment} other{will create # adjustments}}",
"op.stock.inventory.reasonBase": "Inventory"
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json`** (реальный таджикский, НЕ копия ru):

```
"op.stock.tab.inventory": "Барӯйхатгирӣ",
"op.stock.inventory.noPermission": "Барои барӯйхатгирӣ ҳуқуқ нокифоя",
"op.stock.inventory.loading": "Боргузорӣ…",
"op.stock.inventory.title": "Аз нав ҳисобкунии бақияҳо",
"op.stock.inventory.scanHint": "Молро барои аз нав ҳисобкунӣ сканер кунед — сатр равшан мешавад, миқдори воқеиро ворид кунед",
"op.stock.inventory.search": "Ҷустуҷӯи мол",
"op.stock.inventory.empty": "Моли дорои ҳисоби бақия нест",
"op.stock.inventory.colItem": "Мол",
"op.stock.inventory.colSystem": "Ҳисобӣ",
"op.stock.inventory.colFact": "Воқеӣ дар раф",
"op.stock.inventory.colDiff": "Фарқият",
"op.stock.inventory.colSum": "Маблағ",
"op.stock.inventory.notCounted": "ҳисоб нашуд",
"op.stock.inventory.reset": "Аз нав",
"op.stock.inventory.progressTitle": "Раванди аз нав ҳисобкунӣ",
"op.stock.inventory.counted": "Ҳисоб шуд",
"op.stock.inventory.totalTitle": "Натиҷаи барӯйхатгирӣ",
"op.stock.inventory.discrepancies": "Фарқиятҳо",
"op.stock.inventory.shortage": "Камомад",
"op.stock.inventory.surplus": "Зиёдатӣ",
"op.stock.inventory.netCost": "Натиҷа аз рӯи арзиши аслӣ",
"op.stock.inventory.post": "Барӯйхатгириро гузаронидан",
"op.stock.inventory.posting": "Гузаронида истодааст…",
"op.stock.inventory.posted": "Барӯйхатгирӣ гузаронида шуд: {count, plural, other{# ислоҳ}}",
"op.stock.inventory.partial": "Гузаронида шуд {posted} аз {total} — барои боқимонда такрор кунед",
"op.stock.inventory.willCreate": "{count, plural, =0{фарқият нест} other{# ислоҳ эҷод мекунад}}",
"op.stock.inventory.reasonBase": "Барӯйхатгирӣ"
```

- [ ] **Step 4: Регенерировать messages** — `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`. Ожидается: обновлён `packages/i18n/src/messages.ts` без ошибок (новые ключи во всех локалях, union `MessageKey` расширен).

- [ ] **Step 5: Прогнать i18n-гарды** — `cd packages/i18n && /home/fedya/.bun/bin/bun test`. Ожидается: PASS, включая `tg≠ru` honesty-guard (новые tg-строки отличаются от ru) и key-existence guard.

- [ ] **Step 6: Commit** — `git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts && git commit -m "feat(i18n): ключи инвентаризации склада (ru/en/tg)"`

---

## Task 3: Экран «Инвентаризация» + стили

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css`

**Interfaces:**
- Consumes: модель из Task 1; i18n-ключи из Task 2; `createAuthenticatedOperatorClients`/`createIdempotencyKey`/`readArray`/`readBoolean`/`readString`/`requireBackend` из `../operatorHelpers`; `formatMinorUnits` из `../currencyFormat`; `projectOperatorError` из `../apiErrors`; `hasPermission`/`permissionNames` из `../operatorPermissions`; `matchByBarcode` из `../barcodeScanner`; `useBarcodeScanner` из `../useBarcodeScanner`; `useToast` из `../operatorToast`; `api.inventory.createStockMovement` (поле `quantityDelta`, `unitCost: MoneyDto`, `reason`, `idempotencyKey`).
- Produces (для Task 4): компонент `InventoryWorkspace` с пропсами `{ backend, currencyCode, session }`.

- [ ] **Step 1: Write the failing test** — `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx`

```ts
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { act, render, screen, fireEvent, cleanup, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, avgCostMinorUnits: 400, barcodes: ['111'], price: { currencyCode: 'TJS', minorUnits: 1000 } },
  { productId: 'p2', name: 'Вода 0.5', sku: 'WATER-05', trackStock: true, stockOnHand: 30, avgCostMinorUnits: 200, barcodes: ['222'], price: { currencyCode: 'TJS', minorUnits: 600 } },
  { productId: 'p3', name: 'Время-услуга', sku: 'TIME', trackStock: false, stockOnHand: 0, avgCostMinorUnits: 0, barcodes: [], price: { currencyCode: 'TJS', minorUnits: 0 } },
]));
const createStockMovement = mock(async (_branchId: string, _request: Record<string, unknown>) => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }));

const { InventoryWorkspace } = await import('./InventoryWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const manageSession = { permissions: ['inventory.view', 'inventory.stock.manage'], organizationId: 'o' } as never;

const view = (props: Record<string, unknown> = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <InventoryWorkspace backend={backend} currencyCode="TJS" session={manageSession} {...props} />
      </ToastProvider>
    </I18nProvider>
  );

function scan(code: string) {
  act(() => {
    for (const ch of code) window.dispatchEvent(new KeyboardEvent('keydown', { key: ch, bubbles: true }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  });
}

const factInput = (name: string) => screen.getByLabelText(new RegExp(`Факт по полке: ${name}`)) as HTMLInputElement;

afterEach(() => { createStockMovement.mockClear(); getCatalog.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('InventoryWorkspace', () => {
  it('без права управления — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><ToastProvider><InventoryWorkspace backend={backend} currencyCode="TJS" session={{ permissions: ['inventory.view'], organizationId: 'o' } as never} /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для инвентаризации')).toBeInTheDocument();
  });

  it('рендерит отслеживаемые товары строками с учётным остатком; неотслеживаемые скрыты', async () => {
    view();
    expect(await screen.findByText('Cola 0.5')).toBeInTheDocument();
    expect(screen.getByText('Вода 0.5')).toBeInTheDocument();
    expect(screen.queryByText('Время-услуга')).not.toBeInTheDocument();
  });

  it('ввод факта считает расхождение; кнопка «Провести» включается', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } });
    expect(await screen.findByText('-2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Провести инвентаризацию' })).not.toBeDisabled();
  });

  it('«Провести» шлёт adjustment на каждое расхождение со знаковой дельтой и себест из avg', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } });   // -2
    fireEvent.change(factInput('Вода 0.5'), { target: { value: '33' } });   // +3
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(2));
    const reqs = createStockMovement.mock.calls.map((c) => c[1]);
    const cola = reqs.find((r) => r.productId === 'p1')!;
    expect(cola).toMatchObject({ movementType: 'adjustment', quantityDelta: -2 });
    expect(cola.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    expect(typeof cola.reason).toBe('string');
    expect((cola.reason as string).length).toBeGreaterThan(0);
    const water = reqs.find((r) => r.productId === 'p2')!;
    expect(water).toMatchObject({ movementType: 'adjustment', quantityDelta: 3, reason: 'Инвентаризация' });
  });

  it('строки без факта и с нулевым расхождением не проводятся', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '12' } }); // совпало → пропуск
    // Вода не пересчитана → пропуск
    expect(screen.getByRole('button', { name: 'Провести инвентаризацию' })).toBeDisabled();
  });

  it('частичный сбой: «Проведено X из Y», ретрай проводит только оставшееся', async () => {
    createStockMovement.mockImplementation(async (_b: string, request: Record<string, unknown>) => {
      if (request.productId === 'p2') throw new Error('boom');
      return { stockMovementId: 'ok' };
    });
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } }); // -2 (успех)
    fireEvent.change(factInput('Вода 0.5'), { target: { value: '28' } }); // -2 (упадёт)
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(screen.getByText(/Проведено 1 из 2/)).toBeInTheDocument());
    // Cola теперь сошлась (учётный := факт), повторный прогон шлёт только Воду
    createStockMovement.mockImplementation(async () => ({ stockMovementId: 'ok2' }));
    createStockMovement.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    expect(createStockMovement.mock.calls[0][1]).toMatchObject({ productId: 'p2' });
  });

  it('скан известного штриха фокусирует поле факта своей строки', async () => {
    view();
    await screen.findByText('Cola 0.5');
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    scan('222'); // Вода
    await waitFor(() => expect(document.activeElement).toBe(factInput('Вода 0.5')));
  });

  it('скан неизвестного штриха показывает тост', async () => {
    view();
    await screen.findByText('Cola 0.5');
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    scan('999');
    await waitFor(() => expect(screen.getByText('Штрих-код не привязан')).toBeInTheDocument());
  });

  it('каталог без учётных товаров → пустое состояние', async () => {
    getCatalog.mockImplementationOnce(async () => ([
      { productId: 'x', name: 'Время', sku: 'TIME', trackStock: false, stockOnHand: 0, avgCostMinorUnits: 0, barcodes: [], price: { currencyCode: 'TJS', minorUnits: 0 } },
    ]));
    view();
    expect(await screen.findByText('Нет товаров с учётом остатка')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test, verify it fails** — `cd src/AFK4.Operator.App.Web && $BUN test src/stock/InventoryWorkspace.test.tsx`. Expected: FAIL (module not found).

- [ ] **Step 3: Implement component** — `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx`

```tsx
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, RotateCcw, ScanLine } from 'lucide-react';
import { createAuthenticatedOperatorClients, createIdempotencyKey, readArray, readBoolean, readString, requireBackend } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { matchByBarcode } from '../barcodeScanner';
import { useBarcodeScanner } from '../useBarcodeScanner';
import { useToast } from '../operatorToast';
import type { PosProductDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  mapCatalogToCountLines, setCounted, markFresh, resetCounts, markPosted,
  lineCounted, lineDiff, lineDiffSumMinorUnits, inventoryTotals, inventoryAdjustments,
  type CountLine,
} from './inventoryModel';

type TrackedProduct = PosProductDto & { barcodes: string[] };
type PostState = { kind: 'idle' } | { kind: 'posting' } | { kind: 'done'; count: number } | { kind: 'error'; detail: string };

export function InventoryWorkspace({
  backend,
  currencyCode,
  session,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const canManage = hasPermission(session, permissionNames.manageInventoryStock);

  const clients = useMemo(
    () => (backend && canManage ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canManage]
  );

  const [catalog, setCatalog] = useState<TrackedProduct[]>([]);
  const [lines, setLines] = useState<CountLine[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [post, setPost] = useState<PostState>({ kind: 'idle' });
  const [reloadNonce, setReloadNonce] = useState(0);
  const inputRefs = useRef<Map<string, HTMLInputElement>>(new Map());

  const trackedCatalog = useMemo(() => catalog.filter((p) => readBoolean(p, 'trackStock')), [catalog]);

  useEffect(() => {
    if (!canManage || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((loaded) => {
        if (!alive) return;
        const projected = (loaded as PosProductDto[]).map((p) => ({ ...p, barcodes: readArray<string>(p, 'barcodes') }));
        setCatalog(projected);
        setLines(mapCatalogToCountLines(projected));
        setPost({ kind: 'idle' });
      })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canManage, reloadNonce]);

  const onScan = useCallback((code: string) => {
    const found = matchByBarcode(trackedCatalog, code);
    if (!found) { toast.info(t('op.pos.scan.unknown')); return; }
    const productId = readString(found, 'productId');
    setLines((cur) => markFresh(cur, productId));
    const el = inputRefs.current.get(productId);
    if (el) { el.focus(); el.scrollIntoView?.({ block: 'nearest' }); el.select?.(); }
  }, [trackedCatalog, toast, t]);

  useBarcodeScanner(canManage && !loading, onScan);

  if (!canManage) {
    return <section className="stock-inventory"><p className="workspace-error">{t('op.stock.inventory.noPermission')}</p></section>;
  }
  if (loading) {
    return <div className="stock-layout"><section className="stock-inventory"><p className="workspace-loading">{t('op.stock.inventory.loading')}</p></section></div>;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-inventory"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const totals = inventoryTotals(lines);
  const adjustments = inventoryAdjustments(lines);
  const posting = post.kind === 'posting';
  const pct = totals.trackedCount === 0 ? 0 : Math.round((totals.countedCount / totals.trackedCount) * 100);
  const unit = t('op.stock.col.unit');
  const hasCounts = lines.some((l) => lineCounted(l) !== null);

  const query = search.trim().toLowerCase();
  const visibleLines = query
    ? lines.filter((l) => l.name.toLowerCase().includes(query) || l.sku.toLowerCase().includes(query))
    : lines;

  const signedUnits = (value: number) => (value > 0 ? `+${value}` : String(value));
  const signedSum = (sum: number) => `${sum < 0 ? '-' : '+'}${formatMinorUnits(Math.abs(sum), currencyCode)}`;

  const postInventory = async () => {
    if (adjustments.length === 0 || posting) return;
    const nextBackend = requireBackend(backend, t);
    const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    const reason = t('op.stock.inventory.reasonBase');
    setPost({ kind: 'posting' });
    let posted = 0;
    try {
      for (const adj of adjustments) {
        await api.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: adj.productId,
          movementType: 'adjustment',
          quantityDelta: adj.quantityDelta,
          unitCost: { currencyCode, minorUnits: adj.unitCostMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create'),
        });
        // Учётный := факт, чтобы ретрай при сбое не провёл строку дважды.
        setLines((cur) => markPosted(cur, adj.productId));
        posted += 1;
      }
      setPost({ kind: 'done', count: posted });
      setReloadNonce((n) => n + 1); // свежие учётные остатки + сброс пересчёта
    } catch (error) {
      setPost(posted > 0
        ? { kind: 'error', detail: t('op.stock.inventory.partial', { posted, total: adjustments.length }) }
        : { kind: 'error', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <div className="stock-layout">
      <section className="stock-inventory">
        <div className="inv-scanbar">
          <span className="inv-scanbar-ico"><ScanLine size={18} aria-hidden="true" /></span>
          <span className="inv-scanbar-lbl" aria-label={t('op.pos.scan.active')}>
            {t('op.stock.inventory.scanHint')}<i className="inv-caret" aria-hidden="true" />
          </span>
          <input
            className="inv-search"
            type="search"
            aria-label={t('op.stock.inventory.search')}
            placeholder={t('op.stock.inventory.search')}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
          />
          {hasCounts && (
            <button type="button" className="inv-reset" disabled={posting} onClick={() => setLines((c) => resetCounts(c))}>
              <RotateCcw size={13} aria-hidden="true" />
              {t('op.stock.inventory.reset')}
            </button>
          )}
        </div>

        <div className="recv-doc" aria-label={t('op.stock.inventory.title')}>
          <h2>{t('op.stock.inventory.title')}</h2>
          {lines.length === 0 ? (
            <p className="cash-shift-empty-note">{t('op.stock.inventory.empty')}</p>
          ) : (
            <>
              <div className="inv-cols" aria-hidden="true">
                <span />
                <span>{t('op.stock.inventory.colItem')}</span>
                <span className="r">{t('op.stock.inventory.colSystem')}</span>
                <span className="r">{t('op.stock.inventory.colFact')}</span>
                <span className="r">{t('op.stock.inventory.colDiff')}</span>
                <span className="r">{t('op.stock.inventory.colSum')}</span>
              </div>
              <ul className="inv-lines">
                {visibleLines.map((line) => {
                  const counted = lineCounted(line);
                  const diff = lineDiff(line);
                  const sum = lineDiffSumMinorUnits(line);
                  const hasDiff = diff !== null && diff !== 0;
                  const diffClass = counted === null ? 'none' : diff === 0 ? 'zero' : diff < 0 ? 'minus' : 'plus';
                  return (
                    <li key={line.productId} className={`inv-row${line.fresh ? ' fresh' : ''}${hasDiff ? ' diff' : ''}${counted === null ? ' pending' : ''}`}>
                      <Boxes size={15} aria-hidden="true" />
                      <div className="inv-name">
                        <strong>{line.name}</strong>
                        <em>{line.sku}</em>
                      </div>
                      <div className="inv-sys">{line.systemQty}</div>
                      <div className={`inv-fact${counted === null ? ' empty' : ''}`}>
                        <input
                          inputMode="numeric"
                          aria-label={`${t('op.stock.inventory.colFact')}: ${line.name}`}
                          placeholder="—"
                          value={line.countedText}
                          ref={(el) => { if (el) inputRefs.current.set(line.productId, el); else inputRefs.current.delete(line.productId); }}
                          onChange={(event) => setLines((c) => setCounted(c, line.productId, event.currentTarget.value))}
                        />
                      </div>
                      <div className={`inv-diff ${diffClass}`}>
                        {counted === null ? t('op.stock.inventory.notCounted') : diff === 0 ? '0' : signedUnits(diff)}
                      </div>
                      <div className={`inv-sum ${diffClass}`}>
                        {counted === null || diff === 0 ? '—' : signedSum(sum)}
                      </div>
                    </li>
                  );
                })}
              </ul>
            </>
          )}
        </div>
      </section>

      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.progressTitle')}</h3>
          <div className="inv-prog"><i style={{ width: `${pct}%` }} /></div>
          <div className="inv-progtxt"><span>{t('op.stock.inventory.counted')}</span><b>{totals.countedCount} / {totals.trackedCount}</b></div>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.totalTitle')}</h3>
          <div className="mv"><span>{t('op.stock.inventory.discrepancies')}</span><b>{totals.discrepancies}</b></div>
          <div className="mv"><span>{t('op.stock.inventory.shortage')}</span><b className="warning-text">-{totals.shortageUnits} {unit} · {formatMinorUnits(totals.shortageSumMinorUnits, currencyCode)}</b></div>
          <div className="mv"><span>{t('op.stock.inventory.surplus')}</span><b className="inv-pos">+{totals.surplusUnits} {unit} · {formatMinorUnits(totals.surplusSumMinorUnits, currencyCode)}</b></div>
          <div className="mv recv-grand"><span>{t('op.stock.inventory.netCost')}</span><b className={totals.netSumMinorUnits < 0 ? 'warning-text' : totals.netSumMinorUnits > 0 ? 'inv-pos' : undefined}>{formatMinorUnits(totals.netSumMinorUnits, currencyCode)}</b></div>
          <button type="button" className="ctx-btn" disabled={adjustments.length === 0 || posting} onClick={postInventory}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.inventory.posting') : t('op.stock.inventory.post')}
          </button>
          <p className="ctx-note">{t('op.stock.inventory.willCreate', { count: adjustments.length })}</p>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.inventory.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 4: Add styles** — добавить в конец `src/AFK4.Operator.App.Web/src/styles/22-stock.css`:

```css
/* ── Инвентаризация (S4) ── */
.stock-inventory { display: flex; flex-direction: column; min-width: 0; }
.inv-scanbar { display: flex; align-items: center; gap: 12px; border: 1.5px solid var(--accent); border-radius: var(--radius-md);
  padding: 10px 14px; background: linear-gradient(180deg, rgba(var(--accent-rgb), 0.06), transparent); margin-bottom: 8px; }
.inv-scanbar-ico { width: 34px; height: 34px; border-radius: 8px; display: grid; place-items: center;
  background: var(--surface-accent-soft); color: var(--accent-text); flex: 0 0 auto; }
.inv-scanbar-lbl { flex: 1; min-width: 0; color: var(--text-secondary); font-size: 13px; }
.inv-caret { display: inline-block; width: 2px; height: 14px; background: var(--accent); margin-left: 3px;
  vertical-align: middle; animation: inv-blink 1s step-end infinite; }
@keyframes inv-blink { 50% { opacity: 0; } }
.inv-search { width: min(220px, 30vw); height: 30px; padding: 0 10px; border: 1px solid var(--border-default);
  border-radius: 6px; background: var(--surface-sunken); color: var(--text-primary); font-size: 13px; }
.inv-reset { display: inline-flex; align-items: center; gap: 5px; height: 30px; padding: 0 10px;
  border: 1px solid var(--border-default); border-radius: 6px; background: var(--surface-elevated);
  color: var(--text-secondary); font-size: 12px; cursor: pointer; }
.inv-reset:hover:not(:disabled) { color: var(--text-primary); border-color: var(--border-strong); }

.inv-cols, .inv-row { display: grid; grid-template-columns: 24px minmax(160px, 1fr) 84px 108px 104px 96px; align-items: center; gap: 12px; }
.inv-cols { padding: 2px 8px 8px; color: var(--text-tertiary); font-size: 11px; font-weight: 600;
  text-transform: uppercase; letter-spacing: 0.05em; border-bottom: 1px solid var(--border-default); }
.inv-cols .r { text-align: right; }
.inv-lines { list-style: none; margin: 6px 0 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
.inv-row { padding: 9px 8px; border: 1px solid var(--border-default); border-radius: var(--radius-sm); background: var(--surface-card); }
.inv-row > svg { width: 15px; height: 15px; color: var(--text-tertiary); }
.inv-row.pending { opacity: 0.72; }
.inv-row.diff { border-color: rgba(var(--accent-rgb), 0.35); }
.inv-row.fresh { border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent) inset; }
.inv-name { min-width: 0; }
.inv-name strong { display: block; color: var(--text-primary); font-size: 14px; font-weight: 600; }
.inv-name em { color: var(--text-tertiary); font-family: var(--font-mono); font-size: 11px; font-style: normal; }
.inv-sys { text-align: right; color: var(--text-primary); font-family: var(--font-mono); font-variant-numeric: tabular-nums; font-size: 14px; font-weight: 600; }
.inv-fact { justify-self: end; display: flex; align-items: center; height: 32px; width: 100px;
  border: 1px solid var(--border-default); border-radius: 6px; background: var(--surface-sunken); padding: 0 10px; }
.inv-fact.empty { border-style: dashed; }
.inv-fact input { width: 100%; border: 0; background: transparent; color: var(--text-primary);
  font-family: var(--font-mono); font-variant-numeric: tabular-nums; font-size: 15px; font-weight: 600; text-align: right; }
.inv-fact.empty input::placeholder { color: var(--text-quaternary); }
.inv-diff, .inv-sum { text-align: right; font-family: var(--font-mono); font-variant-numeric: tabular-nums; font-size: 13px; font-weight: 600; }
.inv-diff.zero, .inv-sum.zero { color: var(--text-secondary); }
.inv-diff.none, .inv-sum.none { color: var(--text-tertiary); font-weight: 400; }
.inv-diff.minus, .inv-sum.minus { color: var(--warning-text); }
.inv-diff.plus, .inv-sum.plus { color: var(--accent-text); }

.inv-prog { height: 8px; border-radius: 5px; background: var(--surface-sunken); overflow: hidden; box-shadow: inset 0 0 0 1px var(--border-soft); }
.inv-prog i { display: block; height: 100%; background: var(--accent); border-radius: 5px; transition: width 0.2s ease; }
.inv-progtxt { display: flex; justify-content: space-between; margin-top: 6px; font-size: 12px; color: var(--text-secondary); }
.inv-progtxt b { color: var(--text-primary); font-family: var(--font-mono); }
.inv-pos { color: var(--accent-text); }
```

- [ ] **Step 5: Run test, verify pass** — `cd src/AFK4.Operator.App.Web && $BUN test src/stock/InventoryWorkspace.test.tsx`. Expected: PASS (все кейсы, включая фокус по скану и частичный сбой с ретраем).

- [ ] **Step 6: Commit** — `git add src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.test.tsx src/AFK4.Operator.App.Web/src/styles/22-stock.css && git commit -m "feat(operator): экран инвентаризации — пересчёт со сканером, проведение корректировок"`

---

## Task 4: Подключить вкладку «Инвентаризация» в раздел Склад

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/stockModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx`

**Interfaces:**
- Consumes: `InventoryWorkspace` (Task 3); `op.stock.tab.inventory` (Task 2).
- Produces: вкладка `'inventory'` видна при праве `inventory.stock.manage`.

- [ ] **Step 1: Add failing test** — добавить в `describe('StockWorkspace — вкладки', ...)` в `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx` (и в mock из строки 12 добавить `getStockMovements` НЕ требуется — getCatalog уже есть; InventoryWorkspace зовёт только `pos.getCatalog`):

```ts
  it('вкладка «Инвентаризация» видна при праве управления и переключается', async () => {
    view(manageSession);
    const tab = screen.getByRole('tab', { name: 'Инвентаризация' });
    fireEvent.click(tab);
    expect(tab).toHaveAttribute('aria-selected', 'true');
  });

  it('без права управления вкладка «Инвентаризация» скрыта', () => {
    view(viewOnlySession);
    expect(screen.queryByRole('tab', { name: 'Инвентаризация' })).not.toBeInTheDocument();
  });
```

- [ ] **Step 2: Run test, verify it fails** — `cd src/AFK4.Operator.App.Web && $BUN test src/stock/StockWorkspace.test.tsx`. Expected: FAIL (нет вкладки «Инвентаризация»).

- [ ] **Step 3: Extend `stockModel.ts`** — заменить три определения:

```ts
export type StockTab = 'levels' | 'receiving' | 'journal' | 'inventory';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels', 'receiving', 'journal', 'inventory'];

// Права под-вкладок «Склада»: levels — просмотр инвентаря; receiving/inventory — управление складом (запись); journal — просмотр.
export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  receiving: [permissionNames.manageInventoryStock],
  journal: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  inventory: [permissionNames.manageInventoryStock],
};
```

- [ ] **Step 4: Wire into `StockWorkspace.tsx`** — (a) импорт после `JournalWorkspace`:

```tsx
import { InventoryWorkspace } from './InventoryWorkspace';
```

(b) добавить в `TAB_LABELS`:

```tsx
const TAB_LABELS: Record<StockTab, MessageKey> = {
  levels: 'op.stock.tab.levels',
  receiving: 'op.stock.tab.receiving',
  journal: 'op.stock.tab.journal',
  inventory: 'op.stock.tab.inventory',
};
```

(c) добавить блок монтирования внутри `.cash-tab-content`, после блока `journal`:

```tsx
        {activeTab === 'inventory' && (
          <InventoryWorkspace backend={backend} currencyCode={currencyCode} session={session} />
        )}
```

- [ ] **Step 5: Run test, verify pass** — `cd src/AFK4.Operator.App.Web && $BUN test src/stock/StockWorkspace.test.tsx`. Expected: PASS.

- [ ] **Step 6: Commit** — `git add src/AFK4.Operator.App.Web/src/stock/stockModel.ts src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx && git commit -m "feat(operator): вкладка «Инвентаризация» в разделе Склад"`

---

## Task 5: Epic-уборка — per-product порог низкого остатка в POS

> Спека §5/§7: «убрать хардкод LOW_STOCK_THRESHOLD; per-product порог `ReorderThreshold`, `0` = без алертинга». S0 убрал хардкод только в разделе Склад; POS-каталог (`BackendPosWorkspace`) всё ещё считает `stockOnHand <= 2`. Это последний слайс эпика — закрываем долг.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx` (создать, если отсутствует)

**Interfaces:**
- Consumes: `readNumber` уже импортирован в файле; `PosProductDto` поле `reorderThreshold`.

- [ ] **Step 1: Add failing test** — проверить наличие `BackendPosWorkspace.test.tsx`: `ls src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`. Если есть — добавить тест в существующий describe; если нет — создать минимальный файл по образцу мок-харнеса (`__afk4RealOperatorHelpers` + `mock.module('./operatorHelpers', ...)`, `createAuthenticatedOperatorClients` отдаёт `getCatalog`). Тест проверяет per-product порог через строку низкого остатка (`op.pos.strip.stockLow`):

```ts
// getCatalog отдаёт: p1 stockOnHand=3 reorderThreshold=5 → low; p2 stockOnHand=3 reorderThreshold=2 → ok; p3 stockOnHand=0 reorderThreshold=0 → НЕ алертим (порог 0)
// Ожидание: счётчик низкого остатка = 1 (только p1), НЕ хардкод <=2.
```

(Точную обвязку рендера/моков взять из `ReceivingWorkspace.test.tsx` / соседних POS-тестов; убедиться, что строка `op.pos.strip.stockLow` с count=1 отображается.)

- [ ] **Step 2: Run test, verify it fails** — `cd src/AFK4.Operator.App.Web && $BUN test src/BackendPosWorkspace.test.tsx`. Expected: FAIL (текущий хардкод `<= 2` посчитал бы p2 как низкий → count=2).

- [ ] **Step 3: Add `reorderThreshold` to catalog item** — в `BackendPosWorkspace.tsx`:

(a) тип `PosCatalogItem` (строка ~40, после `stockOnHand: number;`):

```tsx
  stockOnHand: number;
  reorderThreshold: number;
```

(b) фикстуры `makeFixtureProducts` — каждой добавить `reorderThreshold: 0,` (фикстуры без учёта остатка не алертим):

```tsx
    { name: t('op.pos.fixture.cola'), priceMinorUnits: 1200, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.water'), priceMinorUnits: 600, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.hotdog'), priceMinorUnits: 2800, category: t('op.pos.fixture.food'), note: t('op.pos.fixture.note'), stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.guestHour'), priceMinorUnits: 2500, category: t('op.pos.fixture.services'), note: t('op.pos.fixture.note'), stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' }
```

(c) `projectPosProduct` — прочитать порог из DTO (после `const stockOnHand = ...`):

```tsx
  const stockOnHand = readNumber(product, 'stockOnHand', 0);
  const reorderThreshold = readNumber(product, 'reorderThreshold', 0);
```

и в возвращаемом объекте, после `stockOnHand,`:

```tsx
    stockOnHand,
    reorderThreshold,
```

- [ ] **Step 4: Per-product порог в `lowStockCount`** — заменить строку ~219:

```tsx
  const lowStockCount = catalog.filter((product) =>
    product.source === 'backend' && product.reorderThreshold > 0 && product.stockOnHand <= product.reorderThreshold
  ).length;
```

- [ ] **Step 5: Run test, verify pass** — `cd src/AFK4.Operator.App.Web && $BUN test src/BackendPosWorkspace.test.tsx`. Expected: PASS (count=1).

- [ ] **Step 6: Commit** — `git add src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx && git commit -m "fix(operator): POS — per-product порог низкого остатка вместо хардкода (спека §5)"`

---

## Финальный гейт (после всех задач, перед PR)

1. `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test` — весь фронт зелёный.
2. `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build` — `tsc -b && vite build` без ошибок (тайпчек тест-файлов в т.ч.).
3. `cd packages/i18n && /home/fedya/.bun/bin/bun test` — i18n-гарды (tg≠ru honesty, key-existence).
4. Бэк не менялся — sanity: `/home/fedya/.dotnet/dotnet build src/AFK4.Platform.Api` (компиляция). Полный `dotnet test` не обязателен (нет правок бэка), но Windows-джоб (WPF) и бэк-тесты прогонит CI.
5. App.test (если затрагивается) — отдельным `bun test`-прогоном при флаке.

## Заметки для финального ревью (broad)

- **Дизайн-решение (не дефект):** скан в инвентаризации находит+подсвечивает+фокусирует строку, НЕ +1 — спека §2 ограничивает правило «+1» приёмкой/POS; мокап `inventory-count-v2` явно «введите фактическое количество».
- **markPosted-инвариант:** после успешной корректировки учётный := факт (расхождение → 0). Это анти-двойное-проведение при ретрае частичного сбоя. Проверить тестом ретрая.
- **Журнал не менялся:** инвентаризационные `adjustment` падают под чип «Коррекция» с reason-текстом «Инвентаризация» после SKU. Отрицательная коррекция инвентаризации в `summarize` журнала суммируется как «Списано» (недостача = фактическое выбытие — приемлемо).
- **tg НЕ native-reviewed** — реальный таджикский, но требует проверки носителем (как и весь tg-каталог).
- **POS auto-add-first-product (вне S4):** `BackendPosWorkspace` авто-кладёт первый товар каталога в чек при backend-загрузке — странный UX, в бэклог (не чинить здесь без понимания причины).
