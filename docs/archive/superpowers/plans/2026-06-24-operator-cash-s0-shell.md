# Operator «Касса» S0 — каркас раздела (shell + якорь смены) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать распылённые кассовые экраны (pos/shop_orders/payments/review/shifts) в единый раздел «Касса» — новый воркспейс `cash` с шапкой-якорем статуса смены и под-вкладками, переносящими существующие воркспейсы 1:1 без изменения их поведения.

**Architecture:** Новый `WorkspaceId 'cash'` заменяет в навигации пять старых id. Компонент `CashWorkspace` рисует `CashShiftHeader` (read-only статус смены через `shiftRevenue.current`) + полосу под-вкладок (паттерн `ClientDetail`/`.review-segments`) и рендерит существующие воркспейс-компоненты как есть под вкладками. Файлы воркспейсов НЕ перемещаются (исключает каскад сломанных импортов и `mock.module`-путей). Слияние контента вкладок отложено: Платежи+Смены → одна «Смена» в S1; Продажи+Заказы → одна «Продажи» в S3.

**Tech Stack:** React 19, TypeScript, bun test (happy-dom + jest-dom), Vite, `@afk4/i18n` (ICU), `@afk4/tokens`, `@afk4/money`.

## Global Constraints

- **bun не на PATH** — вызывать полным путём `/home/fedya/.bun/bin/bun`. Тесты: `bun test`. Сборка: `bun run build` (= `tsc -b && vite build`; ловит type-ошибки, которые bun-runner пропускает).
- **App.test гонять ОТДЕЛЬНЫМ прогоном** (`bun test src/App.test.tsx`) — `mock.module` течёт process-wide. Скрипт `package.json` `test` уже разделяет subdir-прогон и App.test.
- **Деньги:** DTO в minor units; форматировать ТОЛЬКО через `formatMoney(moneyDto, currencyCode)` из `./operatorHelpers` на UI-границе. Никаких ручных делений на 100.
- **i18n:** локали в repo-root `locales/{ru,en,tg}.json`; после правки регенерировать `packages/i18n/src/messages.ts` через `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`. Каждый новый ключ — во ВСЕХ трёх локалях; tg — реальный таджикский (не копия ru), иначе guard-тест `messages.test.ts` упадёт. `t()` строго типизирован — новый ключ не скомпилируется, пока не добавлен в три локали И не перегенерён `messages.ts`.
- **WorkspaceId — Record-guard:** `workspacePermissionRules: Record<WorkspaceId, readonly string[]>` — TS требует ключ на каждый id. Добавление `'cash'` обяжет добавить ключ; удаление id обяжет убрать ключ и render-блоки. `workspaceIds` (operatorPermissions.ts:4) — отдельный литеральный массив, синхронизировать вручную.
- **Токены `@afk4/tokens`:** отступы `--space-1..6`, высоты контролов `--control-sm/md/lg`; в новом CSS использовать их, не хардкод-пиксели (кроме зеркалирования существующего `.booking-header`).
- **~no-behavior-change:** те же пять экранов с тем же поведением, новая оболочка + шапка статуса смены. Меню схлопывается (группа `cashier` 4 пункта + `shifts` из «Отчётов» → 1 пункт «Касса»).

## File Structure

**Создаются:**
- `src/cash/cashModel.ts` — чистая нормализация `ShiftRevenueDto | null` → `CashHeaderState` (статус/касса/выручка).
- `src/cash/cashModel.test.ts` — тесты нормализации.
- `src/cash/CashShiftHeader.tsx` — шапка-якорь: грузит текущую смену, рендерит статус + метрики (`StateFlag`).
- `src/cash/CashShiftHeader.test.tsx` — тест шапки (injectable client).
- `src/cash/CashWorkspace.tsx` — контейнер: шапка + под-вкладки + рендер существующих воркспейсов.
- `src/cash/CashWorkspace.test.tsx` — тест переключения вкладок.
- `src/styles/21-cash.css` — стили `.cash-head`/`.cash-tabs` (зеркало `.booking-header`/`.client-detail-tabs`).

**Модифицируются:**
- `src/operatorTypes.ts:6` — `WorkspaceId`: +`'cash'`, −`'pos'`/`'shop_orders'`/`'payments'`/`'review'`/`'shifts'`.
- `src/operatorPermissions.ts:4,63-104` — `workspaceIds` массив + `workspacePermissionRules` (ключ `cash` = объединение прав; убрать 5 старых ключей).
- `src/operatorData.ts:83-102` — `navSections`: секция `cashier` → один item `cash`; секция `reports` → убрать `shifts`.
- `src/WorkspaceRouter.tsx:10-20,94-112` — импорт `CashWorkspace`; render-блок `cash`; убрать блоки pos/shop_orders/payments/review/shifts.
- `src/DashboardWorkspace.tsx:302-303` — `controlCards` `onNavigate('pos')`/`('payments')` → `('cash')`.
- `src/devMockBackend.ts:236-250` — ветка `/shifts/revenue/current` → фикстура `ShiftRevenueDto` (открытая смена).
- `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.ts` — ключи `op.cash.*`.
- `src/App.test.tsx` — если ссылается на удалённые id (проверить и обновить).

---

### Task 1: cashModel — нормализация статуса смены

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts`
- Test: `src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts`

**Interfaces:**
- Consumes: `ShiftRevenueDto` from `../operatorApiClients` (поля: `state: string`, `openedAtUtc: string`, `cash.expected: MoneyDto`, `earned.total: MoneyDto`).
- Produces: `buildCashHeader(revenue: ShiftRevenueDto | null): CashHeaderState`; type `CashHeaderState = { isOpen: boolean; openedAtUtc: string | null; cashInHand: Money | null; revenueTotal: Money | null }` where `Money = ShiftRevenueDto['earned']['total']`.

- [ ] **Step 1: Write the failing test**

```ts
// src/cash/cashModel.test.ts
import { describe, it, expect } from 'bun:test';
import { buildCashHeader } from './cashModel';
import type { ShiftRevenueDto } from '../operatorApiClients';

function m(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function openShift(overrides: Partial<ShiftRevenueDto> = {}): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(1000), goods: m(500), total: m(1500) },
    inflow: { cash: m(0), nonCash: m(0), walletTopUps: m(0), directTotal: m(0) },
    cash: { starting: m(10000), expected: m(11500), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null,
    ...overrides
  };
}

describe('buildCashHeader', () => {
  it('открытая смена → isOpen + касса (expected) + выручка (earned.total)', () => {
    const s = buildCashHeader(openShift());
    expect(s.isOpen).toBe(true);
    expect(s.openedAtUtc).toBe('2026-06-24T08:00:00Z');
    expect(s.cashInHand?.minorUnits).toBe(11500);
    expect(s.revenueTotal?.minorUnits).toBe(1500);
  });

  it('null → закрыто, всё пусто', () => {
    const s = buildCashHeader(null);
    expect(s.isOpen).toBe(false);
    expect(s.openedAtUtc).toBeNull();
    expect(s.cashInHand).toBeNull();
    expect(s.revenueTotal).toBeNull();
  });

  it('state !== open (closed) → закрыто', () => {
    const s = buildCashHeader(openShift({ state: 'closed' }));
    expect(s.isOpen).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/cashModel.test.ts`
Expected: FAIL — `Cannot find module './cashModel'`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/cash/cashModel.ts
import type { ShiftRevenueDto } from '../operatorApiClients';

type Money = ShiftRevenueDto['earned']['total'];

export interface CashHeaderState {
  isOpen: boolean;
  openedAtUtc: string | null;
  cashInHand: Money | null;
  revenueTotal: Money | null;
}

// Текущая смена для шапки-якоря. Открыта = state==='open'; всё остальное (null/closed) — касса закрыта.
export function buildCashHeader(revenue: ShiftRevenueDto | null): CashHeaderState {
  if (revenue === null || revenue.state !== 'open') {
    return { isOpen: false, openedAtUtc: null, cashInHand: null, revenueTotal: null };
  }
  return {
    isOpen: true,
    openedAtUtc: revenue.openedAtUtc,
    cashInHand: revenue.cash.expected,
    revenueTotal: revenue.earned.total
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/cashModel.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/cashModel.ts src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts
git commit -m "feat(cash): cashModel — нормализация статуса смены для шапки-якоря"
```

---

### Task 2: i18n-ключи раздела «Касса» + dev-mock выручки смены

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `packages/i18n/src/messages.ts` (регенерация)
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts:236-250`

**Interfaces:**
- Produces: ключи `op.cash.title`, `op.cash.header.open`, `op.cash.header.closed`, `op.cash.metric.inHand`, `op.cash.metric.revenue`. dev-mock-ответ на `/shifts/revenue/current` формы `ShiftRevenueDto`.

- [ ] **Step 1: Добавить ключи в три локали**

В `locales/ru.json` (рядом с другими `op.cash`/`op.shell` ключами — порядок ключей не важен, JSON-объект):
```json
"op.cash.title": "Касса",
"op.cash.header.open": "Смена открыта",
"op.cash.header.closed": "Смена не открыта",
"op.cash.metric.inHand": "В кассе",
"op.cash.metric.revenue": "Выручка",
```
В `locales/en.json`:
```json
"op.cash.title": "Cashier",
"op.cash.header.open": "Shift open",
"op.cash.header.closed": "No open shift",
"op.cash.metric.inHand": "In drawer",
"op.cash.metric.revenue": "Revenue",
```
В `locales/tg.json` (реальный таджикский):
```json
"op.cash.title": "Касса",
"op.cash.header.open": "Баст кушода",
"op.cash.header.closed": "Басти кушода нест",
"op.cash.metric.inHand": "Дар касса",
"op.cash.metric.revenue": "Даромад",
```

- [ ] **Step 2: Регенерировать messages.ts**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `generated .../messages.ts from 3 locales`.

- [ ] **Step 3: Проверить i18n-гейт (паритет + tg-честность)**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun test`
Expected: PASS (паритет всех трёх локалей, tg ≠ ru для новых ключей).

- [ ] **Step 4: Добавить ветку dev-mock для revenue/current**

В `src/devMockBackend.ts` рядом с фикстурой `currentShift()` (≈стр. 81) добавить функцию формы `ShiftRevenueDto`:
```ts
function currentShiftRevenue() {
  const m = (minorUnits: number) => money(minorUnits);
  return {
    shiftId: 'sh1', organizationId: ORG, branchId: BRANCH,
    openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', closedByStaffUserId: null, state: 'open',
    earned: { time: m(82000), goods: m(41000), total: m(123000) },
    inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
    cash: { starting: m(100000), expected: m(190000), counted: null, difference: null },
    openedAtUtc: '2026-05-21T08:00:00Z', closedAtUtc: null
  };
}
```
В `route(pathname, method)` (≈стр. 241, рядом с `/shifts/current`) добавить ДО общей `/shifts/current`-проверки (точное совпадение пути важнее — `/shifts/revenue/current` тоже оканчивается на `/current`, поэтому ставить ВЫШЕ):
```ts
if (pathname.endsWith('/shifts/revenue/current')) return currentShiftRevenue();
if (pathname.endsWith('/shifts/revenue')) return { shifts: [], limit: 20 };
```

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts src/AFK4.Operator.App.Web/src/devMockBackend.ts
git commit -m "feat(cash): i18n-ключи раздела + dev-mock /shifts/revenue/current (открытая смена)"
```

---

### Task 3: CashShiftHeader — шапка-якорь статуса смены

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx`
- Create: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css` (добавить `@import`)
- Test: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.test.tsx`

**Interfaces:**
- Consumes: `buildCashHeader` (Task 1); `createAuthenticatedOperatorClients`, `formatMoney` from `../operatorHelpers`; `StateFlag` from `../operatorPrimitives`; `OperatorBackendContext` from `../operatorTypes`; `ShiftRevenueDto` from `../operatorApiClients`.
- Produces: `CashShiftHeader({ backend, currencyCode, client? }: { backend: OperatorBackendContext | null; currencyCode: string; client?: { current(branchId: string): Promise<ShiftRevenueDto | null> } })`.

- [ ] **Step 1: Write the failing test**

```tsx
// src/cash/CashShiftHeader.test.tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftHeader } from './CashShiftHeader';
import type { ShiftRevenueDto } from '../operatorApiClients';

afterEach(cleanup);

function m(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(1000), goods: m(500), total: m(1500) },
    inflow: { cash: m(0), nonCash: m(0), walletTopUps: m(0), directTotal: m(0) },
    cash: { starting: m(10000), expected: m(11500), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function renderHeader(current: ShiftRevenueDto | null) {
  return render(
    <I18nProvider locale="ru">
      <CashShiftHeader backend={backend} currencyCode="TJS" client={{ current: async () => current }} />
    </I18nProvider>
  );
}

describe('CashShiftHeader', () => {
  it('открытая смена → статус «Смена открыта» + метрики кассы/выручки', async () => {
    renderHeader(openShift());
    await waitFor(() => expect(screen.getByText('Смена открыта')).toBeInTheDocument());
    expect(screen.getByText('В кассе')).toBeInTheDocument();
    expect(screen.getByText('Выручка')).toBeInTheDocument();
  });

  it('нет смены → статус «Смена не открыта», без метрик', async () => {
    renderHeader(null);
    await waitFor(() => expect(screen.getByText('Смена не открыта')).toBeInTheDocument());
    expect(screen.queryByText('В кассе')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftHeader.test.tsx`
Expected: FAIL — `Cannot find module './CashShiftHeader'`.

- [ ] **Step 3: Write the component**

```tsx
// src/cash/CashShiftHeader.tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatMoney } from '../operatorHelpers';
import { StateFlag } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { buildCashHeader } from './cashModel';

interface ShiftRevenueReader {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
}

// Якорь раздела «Касса»: статус текущей смены виден из любой вкладки. На S0 — read-only
// (кнопки Открыть/Закрыть/Внести/Изъять добавляет S1). Грузит revenue best-effort: ошибка → «нет смены».
export function CashShiftHeader({
  backend,
  currencyCode,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  client?: ShiftRevenueReader;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;
  const [revenue, setRevenue] = useState<ShiftRevenueDto | null>(null);

  useEffect(() => {
    if (client === null || backend === null) return undefined;
    let active = true;
    client.current(backend.branchId)
      .then((cur) => { if (active) setRevenue(cur); })
      .catch(() => { if (active) setRevenue(null); });
    return () => { active = false; };
  }, [client, backend?.branchId]);

  const header = buildCashHeader(revenue);

  return (
    <section className="cash-head">
      <h1>
        <strong className="cash-head-name">{t('op.cash.title')}</strong>
        {' · '}
        <span className="cash-head-tagline">
          {header.isOpen ? t('op.cash.header.open') : t('op.cash.header.closed')}
        </span>
      </h1>
      {header.isOpen && (
        <div className="cash-head-metrics">
          <StateFlag label={t('op.cash.metric.inHand')} value={formatMoney(header.cashInHand, currencyCode)} />
          <StateFlag label={t('op.cash.metric.revenue')} value={formatMoney(header.revenueTotal, currencyCode)} />
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Add stylesheet (зеркало `.booking-header`)**

Create `src/styles/21-cash.css`:
```css
/* Раздел «Касса»: шапка-якорь (зеркало .booking-header) + под-вкладки (зеркало .client-detail-tabs). */
.cash-head {
  box-sizing: border-box;
  flex: none;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  min-height: var(--map-toolbar-h);
  margin-bottom: var(--space-2);
  padding: 3px 9px;
  border: 1px solid var(--border-soft);
  border-radius: 7px;
  background: var(--surface-elevated);
}
.cash-head h1 {
  flex: 1;
  min-width: 0;
  margin: 0;
  overflow: hidden;
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 500;
  line-height: 1.2;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cash-head-name { font-weight: 700; }
.cash-head-tagline { color: var(--text-secondary); font-weight: 500; }
.cash-head-metrics { display: flex; align-items: center; flex: none; gap: var(--space-2); }

.cash-tabs {
  display: flex;
  gap: var(--space-2);
  margin-bottom: var(--space-2);
  border-bottom: 1px solid var(--border-soft);
}
.cash-tab {
  appearance: none;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  padding: var(--space-2) var(--space-3);
  min-height: var(--control-md);
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
}
.cash-tab:hover { color: var(--text-primary); }
.cash-tab.active { color: var(--accent-bright); border-bottom-color: var(--accent); }
.cash-tab:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.cash-tab-content { min-height: 0; }
```

Add `@import` to `src/styles.css` after the `18-review.css` line (keep numeric order):
```css
@import './styles/21-cash.css';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftHeader.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.test.tsx src/AFK4.Operator.App.Web/src/styles/21-cash.css src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(cash): CashShiftHeader — шапка-якорь статуса смены + стили"
```

---

### Task 4: CashWorkspace — контейнер с под-вкладками

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.test.tsx`

**Interfaces:**
- Consumes: `CashShiftHeader` (Task 3); existing workspace components `BackendPosWorkspace`, `ShopOrdersWorkspace`, `BackendPaymentsWorkspace`, `ShiftsWorkspace`, `ReviewWorkspace` (all named exports from `../`); `OperatorBackendContext`.
- Produces: `CashWorkspace({ backend, currencyCode }: { backend: OperatorBackendContext | null; currencyCode: string })`; type `CashTab = 'sales' | 'orders' | 'payments' | 'shifts' | 'review'`.

Под-вкладки переносят 5 существующих воркспейсов 1:1 (метки — существующие nav-ключи). Слияние Продажи+Заказы → S3; Платежи+Смены → S1.

- [ ] **Step 1: Write the failing test**

```tsx
// src/cash/CashWorkspace.test.tsx
import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

// Подменяем тяжёлые воркспейсы лёгкими маркерами, чтобы тест проверял ТОЛЬКО оболочку/вкладки.
mock.module('../BackendPosWorkspace', () => ({ BackendPosWorkspace: () => <div>POS_PANE</div> }));
mock.module('../ShopOrdersWorkspace', () => ({ ShopOrdersWorkspace: () => <div>ORDERS_PANE</div> }));
mock.module('../BackendPaymentsWorkspace', () => ({ BackendPaymentsWorkspace: () => <div>PAYMENTS_PANE</div> }));
mock.module('../ShiftsWorkspace', () => ({ ShiftsWorkspace: () => <div>SHIFTS_PANE</div> }));
mock.module('../ReviewWorkspace', () => ({ ReviewWorkspace: () => <div>REVIEW_PANE</div> }));
mock.module('./CashShiftHeader', () => ({ CashShiftHeader: () => <div>HEADER</div> }));

const { CashWorkspace } = await import('./CashWorkspace');

afterEach(cleanup);

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function renderWorkspace() {
  return render(
    <I18nProvider locale="ru">
      <CashWorkspace backend={backend} currencyCode="TJS" />
    </I18nProvider>
  );
}

describe('CashWorkspace', () => {
  it('по умолчанию открыта вкладка Продажи (POS) + есть шапка-якорь', () => {
    renderWorkspace();
    expect(screen.getByText('HEADER')).toBeInTheDocument();
    expect(screen.getByText('POS_PANE')).toBeInTheDocument();
    expect(screen.queryByText('REVIEW_PANE')).not.toBeInTheDocument();
  });

  it('клик по вкладке Проверка показывает ReviewWorkspace', () => {
    renderWorkspace();
    screen.getByRole('tab', { name: 'Проверка' }).click();
    expect(screen.getByText('REVIEW_PANE')).toBeInTheDocument();
    expect(screen.queryByText('POS_PANE')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashWorkspace.test.tsx`
Expected: FAIL — `Cannot find module './CashWorkspace'`.

- [ ] **Step 3: Write the component**

```tsx
// src/cash/CashWorkspace.tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import { CashShiftHeader } from './CashShiftHeader';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';
import { BackendPaymentsWorkspace } from '../BackendPaymentsWorkspace';
import { ShiftsWorkspace } from '../ShiftsWorkspace';
import { ReviewWorkspace } from '../ReviewWorkspace';

export type CashTab = 'sales' | 'orders' | 'payments' | 'shifts' | 'review';

// S0: единый раздел «Касса» = шапка-якорь смены + под-вкладки, переносящие существующие
// воркспейсы 1:1 (без слияния контента). Слияние Платежи+Смены → S1, Продажи+Заказы → S3.
export function CashWorkspace({
  backend,
  currencyCode
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>('sales');

  const tabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.shell.nav.pos') },
    { id: 'orders', label: t('op.shell.nav.shop_orders') },
    { id: 'payments', label: t('op.shell.nav.payments') },
    { id: 'shifts', label: t('op.shifts.nav') },
    { id: 'review', label: t('op.shell.nav.review') }
  ];

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader backend={backend} currencyCode={currencyCode} />
      <div className="cash-tabs" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={activeTab === tab.id}
            className={`cash-tab${activeTab === tab.id ? ' active' : ''}`}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div className="cash-tab-content">
        {activeTab === 'sales' && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} />}
        {activeTab === 'orders' && <ShopOrdersWorkspace backend={backend} />}
        {activeTab === 'payments' && <BackendPaymentsWorkspace currencyCode={currencyCode} backend={backend} />}
        {activeTab === 'shifts' && backend !== null && (
          <ShiftsWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} />
        )}
        {activeTab === 'review' && <ReviewWorkspace currencyCode={currencyCode} backend={backend} />}
      </div>
    </main>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashWorkspace.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashWorkspace.test.tsx
git commit -m "feat(cash): CashWorkspace — оболочка с под-вкладками (перенос воркспейсов 1:1)"
```

---

### Task 5: IA-проводка — заменить 5 WorkspaceId на `cash`

**Files:**
- Modify: `src/operatorTypes.ts:6`
- Modify: `src/operatorPermissions.ts:4,63-104`
- Modify: `src/operatorData.ts:83-102`
- Modify: `src/WorkspaceRouter.tsx:10-20,94-112`
- Modify: `src/DashboardWorkspace.tsx:302-303`
- Modify (если нужно): `src/App.test.tsx`

**Interfaces:**
- Consumes: `CashWorkspace` (Task 4).
- Produces: навигационный id `'cash'`; удаляет id `'pos'`/`'shop_orders'`/`'payments'`/`'review'`/`'shifts'`. После задачи `tsc` должен быть зелёным (Record-guard ловит рассинхрон).

Это атомарная задача: правки связаны типами и компилируются только вместе. Гейт — сборка (`tsc`) + App.test.

- [ ] **Step 1: WorkspaceId union** — `src/operatorTypes.ts:6`

Заменить строку:
```ts
export type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'cash' | 'players' | 'payment_cards' | 'logs' | 'settings' | 'loyalty' | 'news';
```
(убраны `pos`, `shop_orders`, `payments`, `review`, `shifts`; добавлен `cash`.)

- [ ] **Step 2: workspaceIds + workspacePermissionRules** — `src/operatorPermissions.ts`

Строка 4 — заменить массив:
```ts
export const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'cash', 'players', 'payment_cards', 'logs', 'settings', 'loyalty', 'news'];
```
В `workspacePermissionRules` (стр. 63-104) удалить ключи `pos`, `shop_orders`, `payments`, `review`, `shifts` и добавить `cash` (объединение их прав — `hasAnyPermission` открывает кассу при праве хоть на одну под-функцию):
```ts
  cash: [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale,
    permissionNames.viewShift,
    permissionNames.openShift,
    permissionNames.viewReports,
    permissionNames.approveMoneyAction
  ],
```

- [ ] **Step 3: navSections** — `src/operatorData.ts:83-102`

Секцию `cashier` свести к одному item (label переиспользует существующий «Касса»):
```ts
  {
    key: 'cashier',
    labelKey: 'op.shell.navGroup.cashier',
    icon: ReceiptText,
    items: [
      { id: 'cash', labelKey: 'op.shell.navGroup.cashier' }
    ]
  },
```
В секции `reports` удалить item `shifts` (оставить только `dashboard`):
```ts
  {
    key: 'reports',
    labelKey: 'op.shell.navGroup.reports',
    icon: LayoutDashboard,
    items: [
      { id: 'dashboard', labelKey: 'op.shell.nav.dashboard' }
    ]
  },
```

- [ ] **Step 4: WorkspaceRouter** — `src/WorkspaceRouter.tsx`

Заменить импорты переносимых воркспейсов на единый импорт (строки 10-20 — убрать прямые импорты `BackendPosWorkspace`, `ShopOrdersWorkspace`, `BackendPaymentsWorkspace`, `ReviewWorkspace`, `ShiftsWorkspace`, добавить):
```tsx
import { CashWorkspace } from './cash/CashWorkspace';
```
Заменить пять render-блоков (строки 94-112: `pos`/`shop_orders`/`payments`/`review`/`shifts`) одним:
```tsx
{workspace === 'cash' && <CashWorkspace currencyCode={currencyCode} backend={backend} />}
```

- [ ] **Step 5: DashboardWorkspace nav-цели** — `src/DashboardWorkspace.tsx:302-303`

В `controlCards` заменить `onNavigate`-цели `'pos'` и `'payments'` на `'cash'` (карточки ведут в раздел «Касса»). Найти literal'ы `'pos'`/`'payments'` в массиве controlCards (строки ≈302-303) и заменить на `'cash'`.

- [ ] **Step 6: Build — tsc ловит все рассинхроны**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: PASS (`✓ built`). Если `tsc` ругается на оставшийся литерал удалённого id — исправить там же (типичные места: App.test, CommandPalette, DashboardWorkspace). Перечитать ошибку, заменить на `'cash'` или удалить ветку.

- [ ] **Step 7: App.test — обновить ссылки на удалённые id**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Expected: если падает на отсутствии `pos`/`payments`/`review`/`shifts` в навигации — обновить тест на `cash` (открытие раздела «Касса» и проверка вкладок). Привести в зелёное, сохранив смысл проверок (раздел открывается, нужные экраны доступны под вкладками).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorTypes.ts src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(cash): IA-проводка — раздел «Касса» вместо pos/shop_orders/payments/review/shifts в навигации"
```

---

### Task 6: Финальные гейты + dev-превью

**Files:** (проверка, без новых)

- [ ] **Step 1: Полный фронт-сьют + App.test**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
Expected: PASS — subdir-прогон (включая новые cash-тесты) + App.test 0 fail.

- [ ] **Step 2: i18n-гейт**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun test`
Expected: PASS (паритет + tg-честность).

- [ ] **Step 3: Build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: PASS.

- [ ] **Step 4: Бэк-гейт (sanity — бэк не трогали)**

Run: `cd /home/fedya/projects/afk4.net && dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo`
Expected: PASS (без регрессий; S0 фронтовый).

- [ ] **Step 5: dev-превью — глазами**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run dev` → открыть `http://127.0.0.1:5174/`.
Проверить: рельс показывает один пункт «Касса» (вместо группы из 4 + «Смены» в «Отчётах»); открытие «Кассы» показывает шапку-якорь «Касса · Смена открыта · В кассе … · Выручка …» и под-вкладки Продажи/Заказы/Платежи/Смены/Проверка; переключение вкладок рендерит соответствующие экраны; «Отчёты» больше не содержат «Смены».

- [ ] **Step 6: Финальный коммит (если правки по итогам превью)**

```bash
git add -A && git commit -m "chore(cash): S0 — финальная проверка гейтов + полировка превью"
```

---

## Self-Review

**1. Spec coverage** (против `2026-06-24-operator-cash-design.md`, секция «Слайсинг → S0»):
- «новый воркспейс `cash` (WorkspaceId/navItems/permissions/Router)» → Task 5. ✓
- «шапка-якорь CashShiftHeader (статус смены, read-only)» → Task 1 (модель) + Task 3 (компонент). ✓
- «вкладки переносят существующие воркспейсы как есть» → Task 4 (5 под-вкладок 1:1). ✓
- «группа cashier → 1 пункт; shifts убран из Отчётов» → Task 5, Step 3. ✓
- «dev-mock» → Task 2, Step 4 (`/shifts/revenue/current`). ✓
- «токены @afk4/tokens» → Task 3, Step 4 (CSS на `--space-*`/`--control-*`). ✓
- «перенос существующих тестов» → пересмотрено: файлы воркспейсов НЕ перемещаются (Architecture), поэтому `ShiftsWorkspace.test`/`ShopOrdersWorkspace.test` остаются на месте и не ломаются; `posAvailableInShell.test` относится к Settings, не к POS (не трогаем). Новые тесты — cash-папка. ✓ (отклонение от формулировки spec обосновано снижением риска — задокументировано в Architecture.)

**2. Placeholder scan:** код в каждом шаге полный; команды с ожидаемым выводом; нет TBD/TODO/«handle edge cases». Task 5 Step 6/7 содержат условную правку («если tsc/App.test ругается») — это не placeholder, а реакция на компилятор: точные места и действие указаны. ✓

**3. Type consistency:** `CashHeaderState`/`buildCashHeader` (Task 1) ↔ потребление в `CashShiftHeader` (Task 3). `CashShiftHeader`-пропсы (`backend`,`currencyCode`,`client?`) ↔ вызов в `CashWorkspace` (Task 4, без `client` — боевой режим). `CashTab` 5 значений ↔ 5 render-веток. `CashWorkspace`-пропсы (`backend`,`currencyCode`) ↔ вызов в Router (Task 5 Step 4). `ShiftRevenueDto` поля (`state`,`openedAtUtc`,`cash.expected`,`earned.total`) — из разведки `shiftRevenue.ts`. `MoneyDto={currencyCode,minorUnits}` — `formatMoney` читает эти поля. ✓

Замечание для исполнителя: при удалении WorkspaceId возможны «забытые» литералы (`CommandPalette.tsx`, `App.test.tsx`, `DashboardWorkspace.tsx`) — Task 5 Step 6 (`tsc`) их выловит; чинить по факту ошибки компилятора.
