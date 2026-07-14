# Склад S1 — Приёмка (Receiving) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать оператору оприходовать завоз — собрать документ прихода (товар · кол-во · себестоимость), провести его → `purchase`-движения по строкам (бэк уже готов, S0), и сделать реальными S0-заглушки на Остатках (＋ приёмка, − списание, «Оформить приёмку»).

**Architecture:** Чистый фронт поверх готового бэка. Новая вкладка «Приёмка» в разделе «Склад» (`StockWorkspace` поднимает `activeTab` + preload-товар). Проведение = N **последовательных идемпотентных** `createStockMovement(purchase)` по строкам (без batch-эндпоинта — спека: «идемпотентность на каждую строку»). Списание — модалка (`PanelModal`) с одним `adjustment`. Сканер — НЕ здесь (это S3, нужна модель штрихов); в S1 верхняя полоса = ручной поиск-добавление товара.

**Tech Stack:** React 18 + TS, `@afk4/i18n` (ICU; `locales/{ru,en,tg}.json` → `bun run gen` в `packages/i18n`), `@afk4/tokens`, `bun test` (happy-dom + jest-dom), CSS в `src/styles/22-stock.css`.

## Global Constraints

- **Деньги — minor units** на проводе; UI форматирует через `formatMinorUnits(minorUnits, currencyCode)` (из `../currencyFormat`). Поля ввода денег: `parseNonNegativeMoneyInputMinorUnits` (парс) / `formatMoneyInputMinorUnits` (в строку) из `../operatorHelpers`.
- **`price` = вложенный `MoneyDto`** (`readMoney(p,'price')?.minorUnits`); **`avgCostMinorUnits` = ПЛОСКОЕ число** (`readNumber(p,'avgCostMinorUnits',0)`). Не перепутать — на этом был prod-баг в S0.
- **Себестоимость в приёмке преподставляется** из `avgCostMinorUnits` товара (средневзвешенная закупочная), оператор может поправить.
- **Токены `@afk4/tokens`, тема dark.** Акцент — emerald `var(--accent)`, НЕ синий. Деньги/числа — `text-primary`/`text-strong` моноширинные. **Янтарь (`--warning`) только для предупреждений** (списание/недостача), не для сумм. Красный (`--danger`) — опасные/удаление.
- **Контраст таблиц:** значения — `text-primary`; заголовки колонок и SKU — минимум `text-tertiary` (не `quaternary`); границы строк — `border-default`.
- **Идемпотентность:** каждый POST-движение получает свежий ключ `createIdempotencyKey('stock-movement-create')`. Кнопка «Провести» дизейблится на время проведения (анти-дабл-клик).
- **Права:** Приёмка/списание требуют `permissionNames.manageInventoryStock`. Просмотр Остатков — `viewInventory | manageInventoryStock` (как в S0).
- **Никаких заглушек/мёртвых контролов** в результате слайса (S0-стабы ＋/−/«Оформить приёмку» становятся реальными; «Сохранить черновик» из мокапа НЕ рисуем — нет персистентности).
- **i18n:** новые `t()`-ключи обязаны существовать в каталоге (guard `i18nKeysExist.test.ts`); новые ru-ключи реально переведены на tg (анти `tg===ru` guard). Порядок: правка `locales/{ru,en,tg}.json` → `cd packages/i18n && bun run gen` → коммит `messages.ts` вместе с локалями.
- **Тест-паттерн (happy-dom):** мок клиентов через `mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }))`, где `actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers`. `afterEach(cleanup); afterAll(mock.restore)`.

---

### Task 1: Чистая логика накладной (`receivingModel.ts`)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/receivingModel.ts`
- Test: `src/AFK4.Operator.App.Web/src/stock/receivingModel.test.ts`

**Interfaces:**
- Consumes: `PosProductDto` (из `../operatorApiClients`), хелперы `readString`/`readNumber`/`formatMoneyInputMinorUnits`/`parseNonNegativeMoneyInputMinorUnits` (из `../operatorHelpers`).
- Produces:
  - `interface ReceiptLine { productId: string; name: string; sku: string; quantity: number; unitCostText: string; fresh: boolean }` — себестоимость хранится **сырым текстом** (свободный ввод без переформатирования на keystroke); minor-значение выводится по требованию.
  - `prefillUnitCostText(product: PosProductDto): string` — преподстановка = `formatMoneyInputMinorUnits(avgCost)` (напр. `'4.00'`)
  - `lineUnitCostMinorUnits(line: ReceiptLine): number` — `parseNonNegativeMoneyInputMinorUnits(unitCostText) ?? 0` (≥0)
  - `addOrAccumulate(lines: ReceiptLine[], product: PosProductDto): ReceiptLine[]`
  - `setQuantity(lines: ReceiptLine[], productId: string, quantity: number): ReceiptLine[]`
  - `setUnitCostText(lines: ReceiptLine[], productId: string, text: string): ReceiptLine[]`
  - `removeLine(lines: ReceiptLine[], productId: string): ReceiptLine[]`
  - `lineSubtotalMinorUnits(line: ReceiptLine): number` — `quantity * lineUnitCostMinorUnits(line)`
  - `interface ReceiptTotals { positions: number; units: number; sumMinorUnits: number }` + `receiptTotals(lines: ReceiptLine[]): ReceiptTotals`
  - `receiptReason(baseLabel: string, supplier: string, invoiceNo: string): string`

- [ ] **Step 1: Написать падающий тест**

`src/AFK4.Operator.App.Web/src/stock/receivingModel.test.ts`:
```ts
import { describe, it, expect } from 'bun:test';
import {
  addOrAccumulate, setQuantity, setUnitCostText, removeLine,
  lineSubtotalMinorUnits, lineUnitCostMinorUnits, receiptTotals, receiptReason, prefillUnitCostText,
  type ReceiptLine,
} from './receivingModel';

const product = (over: Record<string, unknown> = {}) => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', avgCostMinorUnits: 400, ...over,
}) as never;

describe('receivingModel', () => {
  it('prefillUnitCostText форматирует avgCost, не ниже 0', () => {
    expect(prefillUnitCostText(product())).toBe('4.00');
    expect(prefillUnitCostText(product({ avgCostMinorUnits: -5 }))).toBe('0.00');
    expect(prefillUnitCostText(product({ avgCostMinorUnits: undefined }))).toBe('0.00');
  });

  it('lineUnitCostMinorUnits парсит текст, невалидный/пустой → 0', () => {
    expect(lineUnitCostMinorUnits({ unitCostText: '4.00' } as ReceiptLine)).toBe(400);
    expect(lineUnitCostMinorUnits({ unitCostText: '6.' } as ReceiptLine)).toBe(0);
    expect(lineUnitCostMinorUnits({ unitCostText: '' } as ReceiptLine)).toBe(0);
  });

  it('addOrAccumulate: новый товар → строка qty=1 с преподставленной себестоимостью, fresh=true', () => {
    const lines = addOrAccumulate([], product());
    expect(lines).toHaveLength(1);
    expect(lines[0]).toMatchObject({ productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', quantity: 1, unitCostText: '4.00', fresh: true });
  });

  it('addOrAccumulate: повтор того же товара → +1 к количеству, остальные fresh=false', () => {
    const first = addOrAccumulate([], product({ productId: 'a' }));
    const withSecond = addOrAccumulate(first, product({ productId: 'b', name: 'B' }));
    const accumulated = addOrAccumulate(withSecond, product({ productId: 'a' }));
    const a = accumulated.find((l) => l.productId === 'a')!;
    const b = accumulated.find((l) => l.productId === 'b')!;
    expect(a.quantity).toBe(2);
    expect(a.fresh).toBe(true);
    expect(b.fresh).toBe(false);
  });

  it('setQuantity не опускается ниже 1 и усекает дробь', () => {
    const lines = addOrAccumulate([], product());
    expect(setQuantity(lines, 'p1', 0)[0].quantity).toBe(1);
    expect(setQuantity(lines, 'p1', 7.9)[0].quantity).toBe(7);
  });

  it('setUnitCostText кладёт сырой текст; removeLine убирает строку', () => {
    const lines = addOrAccumulate([], product());
    expect(setUnitCostText(lines, 'p1', '6.5')[0].unitCostText).toBe('6.5');
    expect(removeLine(lines, 'p1')).toHaveLength(0);
  });

  it('lineSubtotalMinorUnits и receiptTotals считают позиции/единицы/сумму', () => {
    const line: ReceiptLine = { productId: 'p1', name: 'X', sku: 'S', quantity: 3, unitCostText: '6.00', fresh: false };
    expect(lineSubtotalMinorUnits(line)).toBe(1800);
    const totals = receiptTotals([line, { ...line, productId: 'p2', quantity: 2, unitCostText: '4.00' }]);
    expect(totals).toEqual({ positions: 2, units: 5, sumMinorUnits: 1800 + 800 });
  });

  it('receiptReason кодирует поставщика/№ накладной, пустые опускает', () => {
    expect(receiptReason('Приёмка', '  ООО Напитки ', ' 42 ')).toBe('Приёмка · ООО Напитки · №42');
    expect(receiptReason('Приёмка', '', '')).toBe('Приёмка');
    expect(receiptReason('Приёмка', 'Поставщик', '')).toBe('Приёмка · Поставщик');
  });
});
```

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/receivingModel.test.ts`
Expected: FAIL (модуль `./receivingModel` не существует).

- [ ] **Step 3: Реализация**

`src/AFK4.Operator.App.Web/src/stock/receivingModel.ts`:
```ts
import type { PosProductDto } from '../operatorApiClients';
import { formatMoneyInputMinorUnits, parseNonNegativeMoneyInputMinorUnits, readNumber, readString } from '../operatorHelpers';

// Строка документа прихода. Себестоимость — сырой текст (свободный ввод без переформатирования
// на keystroke; minor-значение парсится по требованию). fresh — только что добавлена/инкрементирована.
export interface ReceiptLine {
  productId: string;
  name: string;
  sku: string;
  quantity: number;       // > 0
  unitCostText: string;   // редактируемый текст, парсится в minor units по требованию
  fresh: boolean;
}

// Преподстановка себестоимости = средневзвешенная закупочная товара (S0, плоское число) как текст.
export function prefillUnitCostText(product: PosProductDto): string {
  return formatMoneyInputMinorUnits(Math.max(readNumber(product, 'avgCostMinorUnits', 0), 0));
}

// Себестоимость строки в minor units (невалидный/пустой текст → 0).
export function lineUnitCostMinorUnits(line: ReceiptLine): number {
  return Math.max(parseNonNegativeMoneyInputMinorUnits(line.unitCostText) ?? 0, 0);
}

// Добавить товар в накладную: уже есть строкой → +1 к количеству (накопление, как сканер),
// иначе новая строка qty=1 с преподставленной себестоимостью. Возвращает НОВЫЙ массив.
export function addOrAccumulate(lines: ReceiptLine[], product: PosProductDto): ReceiptLine[] {
  const productId = readString(product, 'productId');
  if (lines.some((line) => line.productId === productId)) {
    return lines.map((line) =>
      line.productId === productId
        ? { ...line, quantity: line.quantity + 1, fresh: true }
        : { ...line, fresh: false });
  }
  const line: ReceiptLine = {
    productId,
    name: readString(product, 'name'),
    sku: readString(product, 'sku'),
    quantity: 1,
    unitCostText: prefillUnitCostText(product),
    fresh: true,
  };
  return [...lines.map((existing) => ({ ...existing, fresh: false })), line];
}

export function setQuantity(lines: ReceiptLine[], productId: string, quantity: number): ReceiptLine[] {
  return lines.map((line) =>
    line.productId === productId ? { ...line, quantity: Math.max(1, Math.trunc(quantity)), fresh: false } : line);
}

export function setUnitCostText(lines: ReceiptLine[], productId: string, text: string): ReceiptLine[] {
  return lines.map((line) =>
    line.productId === productId ? { ...line, unitCostText: text, fresh: false } : line);
}

export function removeLine(lines: ReceiptLine[], productId: string): ReceiptLine[] {
  return lines.filter((line) => line.productId !== productId);
}

export function lineSubtotalMinorUnits(line: ReceiptLine): number {
  return line.quantity * lineUnitCostMinorUnits(line);
}

export interface ReceiptTotals {
  positions: number;
  units: number;
  sumMinorUnits: number;
}

export function receiptTotals(lines: ReceiptLine[]): ReceiptTotals {
  return {
    positions: lines.length,
    units: lines.reduce((acc, line) => acc + line.quantity, 0),
    sumMinorUnits: lines.reduce((acc, line) => acc + lineSubtotalMinorUnits(line), 0),
  };
}

// «Накладная» (поставщик/№) кодируется в Reason движения — сущности поставщика нет (YAGNI).
export function receiptReason(baseLabel: string, supplier: string, invoiceNo: string): string {
  const parts = [baseLabel];
  const trimmedSupplier = supplier.trim();
  const trimmedInvoice = invoiceNo.trim();
  if (trimmedSupplier) parts.push(trimmedSupplier);
  if (trimmedInvoice) parts.push(`№${trimmedInvoice}`);
  return parts.join(' · ');
}
```

- [ ] **Step 4: Запустить — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/receivingModel.test.ts`
Expected: PASS (все кейсы зелёные).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/receivingModel.ts src/AFK4.Operator.App.Web/src/stock/receivingModel.test.ts
git commit -m "feat(stock): чистая логика накладной приёмки (receivingModel)"
```

---

### Task 2: Вкладка «Приёмка» в каркасе раздела (`stockModel` + `StockWorkspace`)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/stockModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/stockModel.test.ts` (добавить кейс для receiving)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (ключ `op.stock.tab.receiving`)
- Test: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx` (Create)

**Interfaces:**
- Consumes: `visibleStockTabs` (из `./stockModel`), `StockLevelsWorkspace`, `StockTabBar`.
- Produces:
  - `StockTab = 'levels' | 'receiving'`
  - `StockWorkspace` поднимает `activeTab: StockTab` и `receivePreload: { productId: string } | null`; рендерит `ReceivingWorkspace` (создаётся в Task 3) под `receiving`. **На время Task 2 `ReceivingWorkspace` ещё нет — под `receiving` рендерить временную заглушку `<div className="stock-receiving-pending" />`, которую Task 3 заменит.** Передаёт в `StockLevelsWorkspace` новый проп `onReceive?: (productId?: string) => void` (в Task 4 он будет использован; в Task 2 — прокинуть и проигнорировать в Levels, чтобы тип совпал).

- [ ] **Step 1: Добавить ключ вкладки в локали**

В `locales/ru.json` рядом с `"op.stock.tab.levels"`:
```json
  "op.stock.tab.receiving": "Приёмка",
```
В `locales/en.json`:
```json
  "op.stock.tab.receiving": "Receiving",
```
В `locales/tg.json` (реальный таджикский — «қабул» = приём):
```json
  "op.stock.tab.receiving": "Қабули мол",
```
Затем регенерировать каталог:
```bash
cd packages/i18n && bun run gen && cd ../..
```

- [ ] **Step 2: Написать падающий тест каркаса**

`src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx`:
```ts
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } },
]));
const createStockMovement = mock(async () => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }));

const { StockWorkspace } = await import('./StockWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const manageSession = { permissions: ['inventory.view', 'inventory.stock.manage'], organizationId: 'o' } as never;
const viewOnlySession = { permissions: ['inventory.view'], organizationId: 'o' } as never;

const view = (session: unknown) =>
  render(<I18nProvider initialLocale="ru"><StockWorkspace backend={backend} currencyCode="TJS" session={session as never} /></I18nProvider>);

afterEach(() => cleanup());
afterAll(() => mock.restore());

describe('StockWorkspace — вкладки', () => {
  it('при праве на управление видны обе вкладки и можно переключиться на Приёмку', async () => {
    view(manageSession);
    expect(screen.getByRole('tab', { name: 'Остатки' })).toBeInTheDocument();
    const receivingTab = screen.getByRole('tab', { name: 'Приёмка' });
    fireEvent.click(receivingTab);
    expect(receivingTab).toHaveAttribute('aria-selected', 'true');
  });

  it('без права управления вкладка Приёмка скрыта (полоса вкладок не показывается)', () => {
    view(viewOnlySession);
    expect(screen.queryByRole('tab', { name: 'Приёмка' })).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Запустить — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockWorkspace.test.tsx`
Expected: FAIL (вкладки «Приёмка» нет; `receiving` не в модели).

- [ ] **Step 4: Расширить `stockModel.ts`**

```ts
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorAuthSession } from '../authClient';

export type StockTab = 'levels' | 'receiving';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels', 'receiving'];

// Права под-вкладок «Склада»: levels — просмотр инвентаря; receiving — управление складом (запись).
export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  receiving: [permissionNames.manageInventoryStock],
};

export function visibleStockTabs(session: OperatorAuthSession | null): StockTab[] {
  return STOCK_TAB_ORDER.filter((tab) => hasAnyPermission(session, STOCK_TAB_PERMISSIONS[tab]));
}
```

Добавить кейс в `src/AFK4.Operator.App.Web/src/stock/stockModel.test.ts` (рядом с существующими) — управленец видит обе вкладки, наблюдатель только `levels`:
```ts
it('receiving виден только при праве управления складом', () => {
  expect(visibleStockTabs({ permissions: ['inventory.stock.manage'] } as never)).toContain('receiving');
  expect(visibleStockTabs({ permissions: ['inventory.view'] } as never)).not.toContain('receiving');
});
```
(Если в существующем тест-файле нет импорта `visibleStockTabs` — добавить его в строку импорта.)

- [ ] **Step 5: Поднять состояние в `StockWorkspace.tsx`**

```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { StockTabBar } from './StockTabBar';
import { StockLevelsWorkspace } from './StockLevelsWorkspace';
import { visibleStockTabs, type StockTab } from './stockModel';

const TAB_LABELS: Record<StockTab, MessageKey> = {
  levels: 'op.stock.tab.levels',
  receiving: 'op.stock.tab.receiving',
};

// Раздел «Склад» — шапка-якорь + вкладки + активное содержимое. Поднимает activeTab и
// preload-товар: с Остатков можно «уйти в Приёмку» с уже добавленным товаром (Task 4).
export function StockWorkspace({
  currencyCode,
  backend,
  session,
}: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const visible = visibleStockTabs(session);
  const [activeTab, setActiveTab] = useState<StockTab>(() => visible[0] ?? 'levels');
  const [receivePreload, setReceivePreload] = useState<{ productId: string } | null>(null);
  const tabs = visible.map((id) => ({ id, labelKey: TAB_LABELS[id] }));

  // Переход «оформить приёмку» с Остатков: переключить вкладку + (опц.) преподставить товар.
  const goToReceiving = (productId?: string) => {
    if (!visible.includes('receiving')) return;
    setReceivePreload(productId ? { productId } : null);
    setActiveTab('receiving');
  };

  return (
    <main className="workspace-screen stock-screen">
      <div className="cash-head">
        <h1>
          <span className="cash-head-name">{t('op.stock.title')}</span>
        </h1>
      </div>
      {tabs.length > 1 && <StockTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} />}
      <div className="cash-tab-content">
        {activeTab === 'levels' && (
          <StockLevelsWorkspace
            backend={backend}
            currencyCode={currencyCode}
            session={session}
            onReceive={visible.includes('receiving') ? goToReceiving : undefined}
          />
        )}
        {activeTab === 'receiving' && (
          // Task 3 заменит заглушку на <ReceivingWorkspace …/>.
          <div className="stock-receiving-pending" />
        )}
      </div>
    </main>
  );
}
```

Добавить опциональный проп в `StockLevelsWorkspace` (сигнатуру), пока не используя его в рендере (Task 4 подключит):
в `StockLevelsWorkspace.tsx` расширить деструктуризацию пропсов на `onReceive` и тип:
```tsx
export function StockLevelsWorkspace({
  backend,
  currencyCode,
  session,
  onReceive,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  onReceive?: (productId?: string) => void;
}) {
```
(`onReceive` пока не вызывается — линт `no-unused-vars` для пропа в деструктуризации не срабатывает; если CI ругается, добавить `// eslint-disable-next-line @typescript-eslint/no-unused-vars` над строкой `onReceive,` — но сначала проверить, ругается ли.)

- [ ] **Step 6: Запустить тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockWorkspace.test.tsx src/stock/stockModel.test.ts`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): вкладка «Приёмка» в каркасе раздела (модель + поднятое состояние)"
```

---

### Task 3: Экран «Приёмка» (`ReceivingWorkspace`) — UI + проведение

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx` (заменить заглушку на реальный компонент)
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (стили приёмки)
- Modify: `locales/{ru,en,tg}.json` (+ ключи `op.stock.receiving.*`)
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts` (POST stock-movements → эхо движения для превью)

**Interfaces:**
- Consumes: `receivingModel` (Task 1), `createAuthenticatedOperatorClients`/`createIdempotencyKey`/`requireBackend`/`parseNonNegativeMoneyInputMinorUnits`/`formatMoneyInputMinorUnits`/`readBoolean`/`readString` (из `../operatorHelpers`), `formatMinorUnits` (из `../currencyFormat`), `projectOperatorError` (из `../apiErrors`), `hasPermission`/`permissionNames`.
- Produces: `ReceivingWorkspace` со пропсами `{ backend, currencyCode, session, preload, onConsumePreload }`.

- [ ] **Step 1: Добавить ключи `op.stock.receiving.*` в локали + gen**

`locales/ru.json`:
```json
  "op.stock.receiving.title": "Приёмка поставки",
  "op.stock.receiving.addLabel": "Добавить товар",
  "op.stock.receiving.addHint": "Найдите товар по названию или SKU — он добавится в накладную",
  "op.stock.receiving.search": "Название или SKU…",
  "op.stock.receiving.noResults": "Товары не найдены",
  "op.stock.receiving.linesTitle": "Позиции прихода",
  "op.stock.receiving.colItem": "Товар",
  "op.stock.receiving.colQty": "Кол-во",
  "op.stock.receiving.colCost": "Себестоимость ед.",
  "op.stock.receiving.colSum": "Сумма",
  "op.stock.receiving.remove": "Убрать строку",
  "op.stock.receiving.empty": "Накладная пуста — добавьте товары сверху",
  "op.stock.receiving.invoiceTitle": "Накладная",
  "op.stock.receiving.supplier": "Поставщик",
  "op.stock.receiving.invoiceNo": "№ накладной",
  "op.stock.receiving.invoiceNoHint": "необязательно",
  "op.stock.receiving.totalTitle": "Итог приёмки",
  "op.stock.receiving.totalPositions": "Позиций",
  "op.stock.receiving.totalUnits": "Единиц",
  "op.stock.receiving.totalSum": "Сумма",
  "op.stock.receiving.post": "Провести приёмку",
  "op.stock.receiving.posting": "Проведение…",
  "op.stock.receiving.posted": "Приёмка проведена: {count} поз.",
  "op.stock.receiving.partial": "Проведено {posted} из {total} — повторите для остальных",
  "op.stock.receiving.reasonBase": "Приёмка",
  "op.stock.receiving.noPermission": "Недостаточно прав для приёмки",
  "op.stock.receiving.loading": "Загрузка товаров…",
  "op.stock.receiving.noTracked": "Нет товаров с учётом остатка",
```
`locales/en.json` (те же ключи, англ.):
```json
  "op.stock.receiving.title": "Supply receiving",
  "op.stock.receiving.addLabel": "Add product",
  "op.stock.receiving.addHint": "Find a product by name or SKU — it will be added to the receipt",
  "op.stock.receiving.search": "Name or SKU…",
  "op.stock.receiving.noResults": "No products found",
  "op.stock.receiving.linesTitle": "Receipt lines",
  "op.stock.receiving.colItem": "Product",
  "op.stock.receiving.colQty": "Qty",
  "op.stock.receiving.colCost": "Unit cost",
  "op.stock.receiving.colSum": "Sum",
  "op.stock.receiving.remove": "Remove line",
  "op.stock.receiving.empty": "Receipt is empty — add products above",
  "op.stock.receiving.invoiceTitle": "Invoice",
  "op.stock.receiving.supplier": "Supplier",
  "op.stock.receiving.invoiceNo": "Invoice no.",
  "op.stock.receiving.invoiceNoHint": "optional",
  "op.stock.receiving.totalTitle": "Receipt total",
  "op.stock.receiving.totalPositions": "Positions",
  "op.stock.receiving.totalUnits": "Units",
  "op.stock.receiving.totalSum": "Sum",
  "op.stock.receiving.post": "Post receipt",
  "op.stock.receiving.posting": "Posting…",
  "op.stock.receiving.posted": "Receipt posted: {count} item(s)",
  "op.stock.receiving.partial": "Posted {posted} of {total} — retry the rest",
  "op.stock.receiving.reasonBase": "Receiving",
  "op.stock.receiving.noPermission": "Not enough rights to receive stock",
  "op.stock.receiving.loading": "Loading products…",
  "op.stock.receiving.noTracked": "No stock-tracked products",
```
`locales/tg.json` (реальный таджикский):
```json
  "op.stock.receiving.title": "Қабули мол",
  "op.stock.receiving.addLabel": "Иловаи мол",
  "op.stock.receiving.addHint": "Молро аз рӯи ном ё SKU ёбед — он ба фактура илова мешавад",
  "op.stock.receiving.search": "Ном ё SKU…",
  "op.stock.receiving.noResults": "Мол ёфт нашуд",
  "op.stock.receiving.linesTitle": "Сатрҳои қабул",
  "op.stock.receiving.colItem": "Мол",
  "op.stock.receiving.colQty": "Шумора",
  "op.stock.receiving.colCost": "Арзиши аслии воҳид",
  "op.stock.receiving.colSum": "Маблағ",
  "op.stock.receiving.remove": "Сатрро тоза кардан",
  "op.stock.receiving.empty": "Фактура холӣ аст — молҳоро аз боло илова кунед",
  "op.stock.receiving.invoiceTitle": "Фактура",
  "op.stock.receiving.supplier": "Таъминкунанда",
  "op.stock.receiving.invoiceNo": "Рақами фактура",
  "op.stock.receiving.invoiceNoHint": "ихтиёрӣ",
  "op.stock.receiving.totalTitle": "Ҷамъи қабул",
  "op.stock.receiving.totalPositions": "Мавқеъҳо",
  "op.stock.receiving.totalUnits": "Воҳидҳо",
  "op.stock.receiving.totalSum": "Маблағ",
  "op.stock.receiving.post": "Қабулро сабт кардан",
  "op.stock.receiving.posting": "Сабт шуда истодааст…",
  "op.stock.receiving.posted": "Қабул сабт шуд: {count} мавқеъ",
  "op.stock.receiving.partial": "{posted} аз {total} сабт шуд — барои боқимонда такрор кунед",
  "op.stock.receiving.reasonBase": "Қабул",
  "op.stock.receiving.noPermission": "Барои қабул ҳуқуқ нокифоя аст",
  "op.stock.receiving.loading": "Боркунии молҳо…",
  "op.stock.receiving.noTracked": "Моли бо ҳисоби бақия нест",
```
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 2: Написать падающий тест экрана**

`src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx`:
```ts
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS', trackStock: true, stockOnHand: 3, reorderThreshold: 5, avgCostMinorUnits: 600, price: { currencyCode: 'TJS', minorUnits: 1200 } },
  { productId: 'p3', name: 'Время-услуга', sku: 'TIME', trackStock: false, stockOnHand: 0, reorderThreshold: 0, avgCostMinorUnits: 0, price: { currencyCode: 'TJS', minorUnits: 0 } },
]));
const createStockMovement = mock(async () => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }));

const { ReceivingWorkspace } = await import('./ReceivingWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view', 'inventory.stock.manage'], organizationId: 'o' } as never;

const view = (props: Record<string, unknown> = {}) =>
  render(<I18nProvider initialLocale="ru"><ReceivingWorkspace backend={backend} currencyCode="TJS" session={session} preload={null} onConsumePreload={() => {}} {...props} /></I18nProvider>);

afterEach(() => { createStockMovement.mockClear(); getCatalog.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('ReceivingWorkspace', () => {
  it('поиск добавляет товар строкой с преподставленной себестоимостью (avgCost)', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    // строка появилась; себестоимость преподставлена 4.00 (400 minor)
    const lines = await screen.findByLabelText('Позиции прихода');
    expect(within(lines).getByText('Cola 0.5')).toBeInTheDocument();
    const costInput = within(lines).getByLabelText('Себестоимость ед.') as HTMLInputElement;
    expect(costInput.value).toBe('4.00');
  });

  it('товары без учёта остатка (trackStock=false) в поиск не попадают', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'врем' } });
    expect(await screen.findByText('Товары не найдены')).toBeInTheDocument();
  });

  it('повторное добавление того же товара → +1 к количеству, не новая строка', async () => {
    view();
    const input = await screen.findByLabelText('Добавить товар');
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    const lines = screen.getByLabelText('Позиции прихода');
    const qty = within(lines).getByLabelText('Кол-во') as HTMLInputElement;
    expect(qty.value).toBe('2');
  });

  it('preload добавляет товар при загрузке и зовёт onConsumePreload', async () => {
    const onConsumePreload = mock(() => {});
    view({ preload: { productId: 'p2' }, onConsumePreload });
    expect(await screen.findByText('Чипсы Lays')).toBeInTheDocument();
    expect(onConsumePreload).toHaveBeenCalled();
  });

  it('«Провести» шлёт по purchase-движению на строку и очищает накладную', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Провести приёмку' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    const [, req] = createStockMovement.mock.calls[0];
    expect(req).toMatchObject({ productId: 'p1', movementType: 'purchase', quantityDelta: 1 });
    expect(req.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    // успех: накладная очищена
    expect(await screen.findByText('Накладная пуста — добавьте товары сверху')).toBeInTheDocument();
  });

  it('частичный сбой оставляет непроведённые строки', async () => {
    // p1 успех, p2 — падение
    createStockMovement.mockImplementation(async (_branch: unknown, req: { productId: string }) => {
      if (req.productId === 'p2') throw new Error('boom');
      return { stockMovementId: 'ok' };
    });
    view();
    const input = await screen.findByLabelText('Добавить товар');
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.change(input, { target: { value: 'чипсы' } });
    fireEvent.click(await screen.findByRole('button', { name: /Чипсы Lays/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Провести приёмку' }));
    // p1 ушёл, p2 остался строкой; показано предупреждение
    await waitFor(() => expect(screen.getByText(/Проведено 1 из 2/)).toBeInTheDocument());
    const lines = screen.getByLabelText('Позиции прихода');
    expect(within(lines).queryByText('Cola 0.5')).not.toBeInTheDocument();
    expect(within(lines).getByText('Чипсы Lays')).toBeInTheDocument();
  });

  it('без права управления — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><ReceivingWorkspace backend={backend} currencyCode="TJS" session={{ permissions: ['inventory.view'], organizationId: 'o' } as never} preload={null} onConsumePreload={() => {}} /></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для приёмки')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Запустить — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ReceivingWorkspace.test.tsx`
Expected: FAIL (нет `./ReceivingWorkspace`).

- [ ] **Step 4: Реализация `ReceivingWorkspace.tsx`**

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, Plus, X } from 'lucide-react';
import { createAuthenticatedOperatorClients, createIdempotencyKey, readBoolean, readString, requireBackend } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { PosProductDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  addOrAccumulate, removeLine, setQuantity, setUnitCostText,
  lineSubtotalMinorUnits, lineUnitCostMinorUnits, receiptTotals, receiptReason,
  type ReceiptLine,
} from './receivingModel';

type PostState = { kind: 'idle' } | { kind: 'posting' } | { kind: 'done'; count: number } | { kind: 'error'; detail: string };

export function ReceivingWorkspace({
  backend,
  currencyCode,
  session,
  preload,
  onConsumePreload,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
  preload: { productId: string } | null;
  onConsumePreload: () => void;
}) {
  const { t } = useI18n();
  const canManage = hasPermission(session, permissionNames.manageInventoryStock);

  const clients = useMemo(
    () => (backend && canManage ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canManage]
  );

  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [lines, setLines] = useState<ReceiptLine[]>([]);
  const [search, setSearch] = useState('');
  const [supplier, setSupplier] = useState('');
  const [invoiceNo, setInvoiceNo] = useState('');
  const [post, setPost] = useState<PostState>({ kind: 'idle' });

  // Только товары с учётом остатка — приходовать имеет смысл только их.
  const trackedCatalog = useMemo(() => catalog.filter((p) => readBoolean(p, 'trackStock')), [catalog]);

  useEffect(() => {
    if (!canManage || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    clients.pos.getCatalog(backend.branchId)
      .then((loaded) => { if (alive) setCatalog(loaded as PosProductDto[]); })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canManage]);

  // Преднабор товара (переход с Остатков по ＋). Срабатывает один раз, когда каталог загружен.
  useEffect(() => {
    if (!preload || loading) return;
    const product = trackedCatalog.find((p) => readString(p, 'productId') === preload.productId);
    if (product) setLines((current) => addOrAccumulate(current, product));
    onConsumePreload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preload, loading, trackedCatalog]);

  if (!canManage) {
    return <section className="stock-receiving"><p className="workspace-error">{t('op.stock.receiving.noPermission')}</p></section>;
  }
  if (loading) {
    return <div className="stock-layout"><section className="stock-receiving"><p className="workspace-loading">{t('op.stock.receiving.loading')}</p></section></div>;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-receiving"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const query = search.trim().toLowerCase();
  const results = query
    ? trackedCatalog.filter((p) => readString(p, 'name').toLowerCase().includes(query) || readString(p, 'sku').toLowerCase().includes(query)).slice(0, 6)
    : [];

  const addProduct = (product: PosProductDto) => {
    setLines((current) => addOrAccumulate(current, product));
    setSearch('');
    setPost({ kind: 'idle' });
  };

  const totals = receiptTotals(lines);

  const postReceipt = async () => {
    if (lines.length === 0 || post.kind === 'posting') return;
    const nextBackend = requireBackend(backend, t);
    const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    const reason = receiptReason(t('op.stock.receiving.reasonBase'), supplier, invoiceNo);
    setPost({ kind: 'posting' });
    const remaining = [...lines];
    let posted = 0;
    try {
      for (const line of lines) {
        await api.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: line.productId,
          movementType: 'purchase',
          quantityDelta: line.quantity,
          unitCost: { currencyCode, minorUnits: lineUnitCostMinorUnits(line) },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create'),
        });
        remaining.shift();
        posted += 1;
      }
      setLines([]);
      setSupplier('');
      setInvoiceNo('');
      setPost({ kind: 'done', count: posted });
    } catch (error) {
      setLines(remaining);
      setPost(posted > 0
        ? { kind: 'error', detail: t('op.stock.receiving.partial', { posted, total: lines.length }) }
        : { kind: 'error', detail: projectOperatorError(error, t).detail });
    }
  };

  const posting = post.kind === 'posting';

  return (
    <div className="stock-layout">
      {/* ── Документ прихода ── */}
      <section className="stock-receiving">
        {/* Полоса добавления товара (в S3 сюда подключится сканер) */}
        <div className="recv-add">
          <div className="recv-add-ico"><Boxes size={20} aria-hidden="true" /></div>
          <div className="recv-add-field">
            <input
              type="search"
              aria-label={t('op.stock.receiving.addLabel')}
              placeholder={t('op.stock.receiving.search')}
              value={search}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
            <span className="recv-add-hint">{t('op.stock.receiving.addHint')}</span>
          </div>
        </div>
        {query && (
          <ul className="recv-results">
            {results.length === 0
              ? <li className="recv-noresults">{t('op.stock.receiving.noResults')}</li>
              : results.map((product) => (
                <li key={readString(product, 'productId')}>
                  <button type="button" onClick={() => addProduct(product)}>
                    <Plus size={14} aria-hidden="true" />
                    <strong>{readString(product, 'name')}</strong>
                    <em>{readString(product, 'sku')}</em>
                  </button>
                </li>
              ))}
          </ul>
        )}

        <div className="recv-doc" aria-label={t('op.stock.receiving.linesTitle')}>
          <h2>{t('op.stock.receiving.linesTitle')}</h2>
          {lines.length === 0 ? (
            <p className="cash-shift-empty-note">{t('op.stock.receiving.empty')}</p>
          ) : (
            <>
              <div className="recv-cols" aria-hidden="true">
                <span />
                <span>{t('op.stock.receiving.colItem')}</span>
                <span>{t('op.stock.receiving.colQty')}</span>
                <span className="r">{t('op.stock.receiving.colCost')}</span>
                <span className="r">{t('op.stock.receiving.colSum')}</span>
                <span />
              </div>
              <ul className="recv-lines">
                {lines.map((line) => (
                  <li key={line.productId} className={`recv-row${line.fresh ? ' fresh' : ''}`}>
                    <Boxes size={15} aria-hidden="true" />
                    <div className="recv-name">
                      <strong>{line.name}</strong>
                      <em>{line.sku}</em>
                    </div>
                    <div className="recv-step">
                      <button type="button" aria-label="−" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity - 1))}>−</button>
                      <input
                        inputMode="numeric"
                        aria-label={t('op.stock.receiving.colQty')}
                        value={String(line.quantity)}
                        onChange={(event) => {
                          const next = Number(event.currentTarget.value);
                          if (Number.isFinite(next)) setLines((c) => setQuantity(c, line.productId, next));
                        }}
                      />
                      <button type="button" aria-label="+" onClick={() => setLines((c) => setQuantity(c, line.productId, line.quantity + 1))}>+</button>
                    </div>
                    <div className="recv-cost">
                      <input
                        inputMode="decimal"
                        aria-label={t('op.stock.receiving.colCost')}
                        value={line.unitCostText}
                        onChange={(event) => setLines((c) => setUnitCostText(c, line.productId, event.currentTarget.value))}
                      />
                      <span className="recv-cost-cur">{currencyCode}</span>
                    </div>
                    <div className="recv-sum">{formatMinorUnits(lineSubtotalMinorUnits(line), currencyCode)}</div>
                    <button type="button" className="recv-del" aria-label={t('op.stock.receiving.remove')} onClick={() => setLines((c) => removeLine(c, line.productId))}>
                      <X size={14} aria-hidden="true" />
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      </section>

      {/* ── Накладная (правая колонка) ── */}
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.invoiceTitle')}</h3>
          <label className="recv-field">
            <span>{t('op.stock.receiving.supplier')}</span>
            <input value={supplier} disabled={posting} onChange={(event) => setSupplier(event.currentTarget.value)} />
          </label>
          <label className="recv-field">
            <span>{t('op.stock.receiving.invoiceNo')}</span>
            <input value={invoiceNo} disabled={posting} placeholder={t('op.stock.receiving.invoiceNoHint')} onChange={(event) => setInvoiceNo(event.currentTarget.value)} />
          </label>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.totalTitle')}</h3>
          <div className="mv"><span>{t('op.stock.receiving.totalPositions')}</span><b>{totals.positions}</b></div>
          <div className="mv"><span>{t('op.stock.receiving.totalUnits')}</span><b>{totals.units}</b></div>
          <div className="mv recv-grand"><span>{t('op.stock.receiving.totalSum')}</span><b>{formatMinorUnits(totals.sumMinorUnits, currencyCode)}</b></div>
          <button type="button" className="ctx-btn" disabled={lines.length === 0 || posting} onClick={postReceipt}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.receiving.posting') : t('op.stock.receiving.post')}
          </button>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.receiving.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 5: Подключить экран в `StockWorkspace.tsx`**

Заменить заглушку из Task 2 на реальный компонент:
```tsx
import { ReceivingWorkspace } from './ReceivingWorkspace';
// …
{activeTab === 'receiving' && (
  <ReceivingWorkspace
    backend={backend}
    currencyCode={currencyCode}
    session={session}
    preload={receivePreload}
    onConsumePreload={() => setReceivePreload(null)}
  />
)}
```

- [ ] **Step 6: Стили приёмки в `src/styles/22-stock.css`** (дописать в конец файла)

```css
/* ── Экран «Приёмка» ── */
.stock-receiving {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  background: var(--surface-elevated);
  min-height: 0;
  overflow: auto;
}

/* Полоса добавления товара */
.recv-add {
  display: flex;
  align-items: center;
  gap: 12px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  padding: 10px 12px;
  background: var(--surface-card);
}

.recv-add-ico {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: grid;
  place-items: center;
  background: var(--surface-accent-soft);
  color: var(--accent-text);
}

.recv-add-field { flex: 1; display: flex; flex-direction: column; gap: 2px; }

.recv-add-field input {
  height: var(--control-md);
  border: 1px solid var(--border-default);
  border-radius: 6px;
  background: var(--surface-sunken);
  color: var(--text-primary);
  padding: 0 10px;
  font-size: 13px;
}

.recv-add-field input:focus { outline: none; border-color: var(--accent); }
.recv-add-hint { color: var(--text-tertiary); font-size: 11px; }

/* Выпадающие результаты поиска */
.recv-results {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
  border: 1px solid var(--border-soft);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
}

.recv-results button {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border: 0;
  background: transparent;
  color: var(--text-primary);
  cursor: pointer;
  font: inherit;
  text-align: left;
}

.recv-results button:hover { background: var(--surface-hover); }
.recv-results button svg { color: var(--accent-text); }
.recv-results button strong { font-size: var(--text-sm); font-weight: 600; }
.recv-results button em { color: var(--text-tertiary); font-family: var(--font-mono); font-size: 11px; font-style: normal; }
.recv-noresults { padding: 10px; color: var(--text-tertiary); font-size: 12px; }

/* Документ прихода */
.recv-doc { display: flex; flex-direction: column; gap: var(--space-1); }
.recv-doc h2 {
  margin: 4px 0 0;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-tertiary);
  font-size: var(--text-xs);
  font-weight: 700;
}

.recv-cols,
.recv-row {
  display: grid;
  grid-template-columns: 24px minmax(140px, 1fr) 116px 132px 90px 36px;
  align-items: center;
  gap: 12px;
}

.recv-cols {
  padding: 4px 8px 6px;
  color: var(--text-tertiary);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid var(--border-soft);
}

.recv-cols .r { text-align: right; }

.recv-lines { list-style: none; margin: 6px 0 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-1); }

.recv-row {
  padding: var(--space-2);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
}

.recv-row.fresh { border-color: var(--accent); box-shadow: inset 0 0 0 1px rgba(var(--accent-rgb), 0.25); }
.recv-row > svg { width: 15px; height: 15px; color: var(--text-tertiary); }
.recv-name { min-width: 0; }
.recv-name strong { display: block; color: var(--text-primary); font-size: var(--text-sm); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.recv-name em { color: var(--text-tertiary); font-family: var(--font-mono); font-size: 10px; font-style: normal; }

/* Степпер количества */
.recv-step { display: inline-flex; align-items: center; }
.recv-step button {
  width: 30px;
  height: 30px;
  border: 1px solid var(--border-default);
  background: var(--surface-elevated);
  color: var(--text-secondary);
  cursor: pointer;
  font-family: var(--font-mono);
  font-size: 15px;
}
.recv-step button:first-of-type { border-radius: 6px 0 0 6px; }
.recv-step button:last-of-type { border-radius: 0 6px 6px 0; }
.recv-step button:hover { border-color: var(--accent); color: var(--accent-bright); }
.recv-step input {
  width: 48px;
  height: 30px;
  border: 1px solid var(--border-default);
  border-left: 0;
  border-right: 0;
  background: var(--surface-sunken);
  color: var(--text-primary);
  text-align: center;
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: 13px;
}

/* Поле себестоимости */
.recv-cost {
  display: flex;
  align-items: center;
  height: 30px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  background: var(--surface-sunken);
  padding: 0 8px;
  gap: 4px;
  justify-self: end;
}
.recv-cost input {
  width: 64px;
  border: 0;
  background: transparent;
  color: var(--text-primary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: 13px;
  text-align: right;
}
.recv-cost input:focus { outline: none; }
.recv-cost-cur { color: var(--text-tertiary); font-size: 11px; }

.recv-sum {
  text-align: right;
  color: var(--text-primary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: var(--text-sm);
  font-weight: 600;
}

.recv-del {
  width: 30px;
  height: 30px;
  border: 1px solid var(--border-soft);
  border-radius: 6px;
  background: var(--surface-elevated);
  color: var(--text-quaternary);
  cursor: pointer;
  display: grid;
  place-items: center;
}
.recv-del:hover { border-color: var(--danger); color: var(--danger); }

/* Поля накладной */
.recv-field { display: grid; gap: 4px; margin-bottom: 10px; }
.recv-field span { color: var(--text-tertiary); font-size: 11px; }
.recv-field input {
  height: var(--control-md);
  border: 1px solid var(--border-default);
  border-radius: 6px;
  background: var(--surface-sunken);
  color: var(--text-primary);
  padding: 0 10px;
  font-size: 13px;
}
.recv-field input:focus { outline: none; border-color: var(--accent); }

.recv-grand { border-top: 1px solid var(--border-soft); margin-top: 4px; padding-top: 10px; }
.recv-grand b { font-size: 18px; }

.recv-status { margin: 10px 0 0; font-size: 12px; }
.recv-status.ok { color: var(--accent-text); }
.recv-status.err { color: var(--danger); }
```

- [ ] **Step 7: Мок POST в `devMockBackend.ts`** (чтобы «Провести» работало в live-превью)

В функции `route(...)` рядом со строкой GET stock-movements добавить ветку POST:
```ts
  if (pathname.endsWith('/inventory/stock-movements') && method === 'POST') return { stockMovementId: 'mock-movement' };
```

- [ ] **Step 8: Запустить тесты экрана**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ReceivingWorkspace.test.tsx`
Expected: PASS (все кейсы).

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ src/AFK4.Operator.App.Web/src/styles/22-stock.css src/AFK4.Operator.App.Web/src/devMockBackend.ts locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): экран приёмки — добавление товара, строки, проведение purchase-движений"
```

---

### Task 4: Связать Остатки → Приёмку (＋ строка / «Оформить приёмку»)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx` (+ кейсы на onReceive)
- Modify: `locales/{ru,en,tg}.json` — поправить `op.stock.summary.orderBtn` использование (ключ уже есть: «Оформить приёмку»).

**Interfaces:**
- Consumes: `onReceive?: (productId?: string) => void` (проп уже добавлен в сигнатуру в Task 2).
- Produces: реальные действия — ＋ на строке зовёт `onReceive(productId)`; «Оформить приёмку» в Сводке зовёт `onReceive()`.

- [ ] **Step 1: Тест на проброс onReceive**

Добавить в `StockLevelsWorkspace.test.tsx`:
```ts
it('кнопка ＋ на строке и «Оформить приёмку» зовут onReceive', async () => {
  const onReceive = mock((_id?: string) => {});
  render(<I18nProvider initialLocale="ru"><StockLevelsWorkspace backend={backend} currencyCode="TJS" session={session} onReceive={onReceive} /></I18nProvider>);
  await screen.findByText('Cola 0.5');
  // ＋ на первой строке (aria-label «Приёмка товара»)
  fireEvent.click(screen.getAllByRole('button', { name: 'Приёмка товара' })[0]);
  expect(onReceive).toHaveBeenCalledWith(expect.any(String));
  // «Оформить приёмку» (есть товары «на исходе» → блок виден)
  fireEvent.click(screen.getByRole('button', { name: 'Оформить приёмку' }));
  expect(onReceive).toHaveBeenCalledWith();
});
```
(Тестовый фикстур уже содержит low/out-товары, поэтому блок «Заказать» с кнопкой рендерится.)

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: FAIL (＋ сейчас `disabled`; кнопка Сводки — `disabled` со строкой `orderBtnSoon`).

- [ ] **Step 3: Сделать ＋ реальным; − оставить пока заглушкой (станет реальным в Task 5)**

В блоке действий строки заменить ＋ на рабочую кнопку (− пока не трогаем — Task 5):
```tsx
<div className="rowact">
  <button
    type="button"
    className="iact"
    disabled={!onReceive}
    title={t('op.stock.action.receive')}
    aria-label={t('op.stock.action.receive')}
    onClick={() => onReceive?.(item.productId)}
  >＋</button>
  <button type="button" className="iact minus" disabled title={t('op.stock.action.writeOff')} aria-label={t('op.stock.action.writeOff')} aria-disabled="true">−</button>
</div>
```

- [ ] **Step 4: Сделать «Оформить приёмку» в Сводке реальной**

Заменить S0-заглушку:
```tsx
<button type="button" className="ctx-btn" disabled={!onReceive} onClick={() => onReceive?.()}>
  {t('op.stock.summary.orderBtn')}
</button>
```

- [ ] **Step 5: Запустить тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx
git commit -m "feat(stock): Остатки → Приёмка (＋ на строке и «Оформить приёмку»)"
```

---

### Task 5: Списание со строки Остатков (`WriteOffDialog`) — убрать последнюю заглушку

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx` (открыть диалог по −, перезагрузить остатки после успеха)
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (стили формы списания, если нужны сверх существующих)
- Modify: `locales/{ru,en,tg}.json` (+ `op.stock.writeoff.*`)

**Interfaces:**
- Consumes: `PanelModal` (из `../PanelModal`), `createAuthenticatedOperatorClients`/`createIdempotencyKey`/`requireBackend`/`readString`/`readNumber` (из `../operatorHelpers`), `formatMinorUnits`.
- Produces: `WriteOffDialog` со пропсами `{ item: StockItem; backend; currencyCode; onClose: () => void; onDone: () => void }`. Шлёт один `adjustment` с `quantityDelta = -qty`, `unitCost = avgCost`, `reason`. `StockLevelsWorkspace` рефетчит каталог после `onDone`.

- [ ] **Step 1: Ключи `op.stock.writeoff.*` + gen**

`locales/ru.json`:
```json
  "op.stock.writeoff.title": "Списание товара",
  "op.stock.writeoff.qty": "Количество к списанию",
  "op.stock.writeoff.reason": "Причина",
  "op.stock.writeoff.reasonPlaceholder": "брак, бой, просрочка…",
  "op.stock.writeoff.available": "На складе: {count} шт.",
  "op.stock.writeoff.cost": "Себестоимость списания",
  "op.stock.writeoff.submit": "Списать",
  "op.stock.writeoff.submitting": "Списание…",
  "op.stock.writeoff.errorQty": "Количество должно быть от 1 до остатка",
  "op.stock.writeoff.errorReason": "Укажите причину списания",
```
`locales/en.json`:
```json
  "op.stock.writeoff.title": "Write off stock",
  "op.stock.writeoff.qty": "Quantity to write off",
  "op.stock.writeoff.reason": "Reason",
  "op.stock.writeoff.reasonPlaceholder": "defect, breakage, expiry…",
  "op.stock.writeoff.available": "In stock: {count} pcs",
  "op.stock.writeoff.cost": "Write-off cost",
  "op.stock.writeoff.submit": "Write off",
  "op.stock.writeoff.submitting": "Writing off…",
  "op.stock.writeoff.errorQty": "Quantity must be between 1 and stock on hand",
  "op.stock.writeoff.errorReason": "Provide a write-off reason",
```
`locales/tg.json`:
```json
  "op.stock.writeoff.title": "Ҳисобборкунии мол",
  "op.stock.writeoff.qty": "Шумора барои ҳисоббор",
  "op.stock.writeoff.reason": "Сабаб",
  "op.stock.writeoff.reasonPlaceholder": "нуқсон, шикаст, мӯҳлаташ гузашта…",
  "op.stock.writeoff.available": "Дар анбор: {count} дона",
  "op.stock.writeoff.cost": "Арзиши ҳисоббор",
  "op.stock.writeoff.submit": "Ҳисоббор кардан",
  "op.stock.writeoff.submitting": "Ҳисоббор шуда истодааст…",
  "op.stock.writeoff.errorQty": "Шумора бояд аз 1 то бақия бошад",
  "op.stock.writeoff.errorReason": "Сабаби ҳисобборро нависед",
```
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 2: Падающий тест диалога**

`src/AFK4.Operator.App.Web/src/stock/WriteOffDialog.test.tsx`:
```ts
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const createStockMovement = mock(async () => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ inventory: { createStockMovement } }) }));

const { WriteOffDialog } = await import('./WriteOffDialog');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const item = { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', stockOnHand: 12, reorderThreshold: 6, priceMinorUnits: 1000, avgCostMinorUnits: 400, category: '' } as never;

const view = (onDone = () => {}, onClose = () => {}) =>
  render(<I18nProvider initialLocale="ru"><WriteOffDialog item={item} backend={backend} currencyCode="TJS" onClose={onClose} onDone={onDone} /></I18nProvider>);

afterEach(() => { createStockMovement.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('WriteOffDialog', () => {
  it('шлёт adjustment с отрицательным кол-вом и себестоимостью из avgCost', async () => {
    const onDone = mock(() => {});
    view(onDone);
    fireEvent.change(screen.getByLabelText('Количество к списанию'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'бой' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    const [, req] = createStockMovement.mock.calls[0];
    expect(req).toMatchObject({ productId: 'p1', movementType: 'adjustment', quantityDelta: -3, reason: 'бой' });
    expect(req.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    expect(onDone).toHaveBeenCalled();
  });

  it('не даёт списать больше остатка', () => {
    view();
    fireEvent.change(screen.getByLabelText('Количество к списанию'), { target: { value: '99' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'бой' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    expect(createStockMovement).not.toHaveBeenCalled();
    expect(screen.getByText('Количество должно быть от 1 до остатка')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Запустить — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/WriteOffDialog.test.tsx`
Expected: FAIL (нет `./WriteOffDialog`).

- [ ] **Step 4: Реализация `WriteOffDialog.tsx`**

```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { createAuthenticatedOperatorClients, createIdempotencyKey, requireBackend } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';
import type { StockItem } from './stockLevels';

export function WriteOffDialog({
  item,
  backend,
  currencyCode,
  onClose,
  onDone,
}: {
  item: StockItem;
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const [qtyText, setQtyText] = useState('1');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const quantity = Number(qtyText);
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > item.stockOnHand) {
      setError(t('op.stock.writeoff.errorQty'));
      return;
    }
    if (!reason.trim()) {
      setError(t('op.stock.writeoff.errorReason'));
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const nextBackend = requireBackend(backend, t);
      const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await api.inventory.createStockMovement(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        productId: item.productId,
        movementType: 'adjustment',
        quantityDelta: -quantity,
        unitCost: { currencyCode, minorUnits: Math.max(item.avgCostMinorUnits, 0) },
        reason: reason.trim(),
        idempotencyKey: createIdempotencyKey('stock-movement-create'),
      });
      onDone();
    } catch (caught) {
      setSubmitting(false);
      setError(projectOperatorError(caught, t).detail);
    }
  };

  return (
    <PanelModal title={t('op.stock.writeoff.title')} subtitle={item.name} tone="warning" onClose={onClose}>
      <div className="recv-field">
        <span>{t('op.stock.writeoff.available', { count: item.stockOnHand })}</span>
      </div>
      <label className="recv-field">
        <span>{t('op.stock.writeoff.qty')}</span>
        <input inputMode="numeric" aria-label={t('op.stock.writeoff.qty')} value={qtyText} disabled={submitting} onChange={(event) => setQtyText(event.currentTarget.value)} />
      </label>
      <label className="recv-field">
        <span>{t('op.stock.writeoff.reason')}</span>
        <input aria-label={t('op.stock.writeoff.reason')} value={reason} disabled={submitting} placeholder={t('op.stock.writeoff.reasonPlaceholder')} onChange={(event) => setReason(event.currentTarget.value)} />
      </label>
      <div className="recv-field">
        <span>{t('op.stock.writeoff.cost')}: {formatMinorUnits(Math.max(item.avgCostMinorUnits, 0), currencyCode)}</span>
      </div>
      {error && <p className="recv-status err" role="alert">{error}</p>}
      <div className="critical-confirmation-actions">
        <button type="button" onClick={onClose} disabled={submitting}>{t('common.cancel')}</button>
        <button type="button" className="danger" disabled={submitting} onClick={submit}>
          {submitting ? t('op.stock.writeoff.submitting') : t('op.stock.writeoff.submit')}
        </button>
      </div>
    </PanelModal>
  );
}
```

- [ ] **Step 5: Подключить − в `StockLevelsWorkspace.tsx` + рефетч**

Вынести загрузку каталога в переиспользуемую функцию и звать её после списания. Минимально: добавить состояние выбранного для списания товара и счётчик перезагрузки.

В начале компонента (рядом с прочими `useState`):
```tsx
const [writeOffItem, setWriteOffItem] = useState<StockItem | null>(null);
const [reloadNonce, setReloadNonce] = useState(0);
```
Добавить `reloadNonce` в зависимости загрузочного `useEffect`:
```tsx
  }, [clients, backend?.branchId, canView, reloadNonce]);
```
Сделать кнопку − рабочей (заменить заглушку из Task 4):
```tsx
<button
  type="button"
  className="iact minus"
  disabled={item.stockOnHand <= 0}
  title={t('op.stock.action.writeOff')}
  aria-label={t('op.stock.action.writeOff')}
  onClick={() => setWriteOffItem(item)}
>−</button>
```
Импортировать диалог и отрендерить его в конце (перед закрывающим `</div>` корня `stock-layout`):
```tsx
import { WriteOffDialog } from './WriteOffDialog';
// …
{writeOffItem && (
  <WriteOffDialog
    item={writeOffItem}
    backend={backend}
    currencyCode={currencyCode}
    onClose={() => setWriteOffItem(null)}
    onDone={() => { setWriteOffItem(null); setReloadNonce((n) => n + 1); }}
  />
)}
```

- [ ] **Step 6: Запустить тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/WriteOffDialog.test.tsx src/stock/StockLevelsWorkspace.test.tsx`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ src/AFK4.Operator.App.Web/src/styles/22-stock.css locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): списание товара со строки Остатков (WriteOffDialog) + рефетч"
```

---

### Task 6: Финальная проверка раздела — сборка, прогон, чистота

**Files:** нет правок кода — только верификация. (Если что-то всплывёт — чинить в этом же таске.)

- [ ] **Step 1: Полный прогон фронт-тестов оператора**

Run: `cd src/AFK4.Operator.App.Web && bun test`
Expected: всё зелёное, включая `i18nKeysExist.test.ts` (используемые ключи существуют) и tg≠ru guard. App.test может потребоваться отдельным прогоном — если падает под полной нагрузкой, запустить `bun test src/App.test.tsx` отдельно.

- [ ] **Step 2: Сборка фронта (tsc + vite)**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: без ошибок типов/сборки.

- [ ] **Step 3: Грязь и сироты**

Проверить, что не осталось: неиспользуемого `orderBtnSoon` (если он больше нигде не нужен — оставить в каталоге, это не сирота кода, но проверить, что в коде он не висит «скоро»-заглушкой); `stock-receiving-pending` заглушки (должна быть заменена в Task 3); ＋/− заглушек на Остатках (обе реальны). `grep -rn "orderBtnSoon\|stock-receiving-pending" src/AFK4.Operator.App.Web/src` — не должно быть мёртвых ссылок.

- [ ] **Step 4: Commit (если были фиксы)**

```bash
git add -A
git commit -m "chore(stock): финальная чистка слайса приёмки"
```
