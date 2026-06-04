# Club Console 7a — Reports + Journal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `/club/reports` screen (5 detailed reports with date range, summary totals, tables, CSV export) and the `/club/journal` audit log screen, on the established club-console pattern.

**Architecture:** Pure model builders + a generic load-only `useReport` hook + a generic `ReportTab` (one component drives all 5 reports via injected loader/builder) + a `ReportsScreen` Tabs shell. Journal mirrors the feature shape: `auditModel` + `useAudit` + `AuditFilters` + `JournalScreen`. Money minor→major and seconds→minutes happen in the model layer (formatters injected for purity). CSV export downloads an authenticated blob via `fetchReportCsv` + a `saveBlob` DOM util.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react, Radix UI (Tabs/Select), shadcn/ui primitives, i18n RU/EN.

**Grounding facts (verified against backend + repo):**
- Report routes: `GET /api/branches/{branchId:guid}/reports/{shifts|sales|gameplay-time|cash-operations|operator-actions}` with optional query `fromUtc`, `toUtc`, `limit`. CSV: same path + `/export.csv`. Permission: `reports.view`.
- Audit route: `GET /api/branches/{branchId:guid}/audit` with optional `action`, `outcome`, `targetType`, `fromUtc`, `toUtc`, `limit`. Permission: `audit.view`. `AuditOutcome` values: `"Succeeded"`, `"Denied"`.
- Backend `MoneyDto` → frontend `MoneyMinor { currencyCode: string; minorUnits: number }` (already in `types.ts`). Money helpers `minorToMajor`/`majorToMinor` in `src/club/money.ts`.
- `useI18n()` → `{ t, formatNumber, formatCurrency(amountMajor, currencyCode), formatDate(iso) }`. `MessageKey` from `@/i18n/messages`.
- Vitest `globals: false` → import `{ it, expect, vi }` from `'vitest'`. Component tests wrap with `<I18nProvider><ToastProvider>…</ToastProvider></I18nProvider>`. Radix Tabs need `fireEvent.mouseDown` then `click`; Radix Select tested at default selection.
- **CRITICAL:** vitest/esbuild does NOT type-check. Only `npm run build` (`tsc -b && vite build`) does. Type mocked fns explicitly: `vi.fn<(a: string) => Promise<X>>()`.
- ui imports: `@/components/ui/{tabs,table,button,card,badge,input,select,states,toast}`; `@/i18n/I18nProvider`.

---

### Task 1: i18n keys (reports + journal) + parity coverage

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts` (add keys to BOTH `ru` and `en` objects)
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.test.ts` (add two coverage blocks)

- [ ] **Step 1: Add the new test coverage blocks first (failing).**

Append to `messages.test.ts`:

```ts
it('includes the reports keys', () => {
  for (const key of [
    'reports.noAccess', 'reports.empty', 'reports.limitNote', 'reports.export', 'reports.export.error',
    'reports.tab.shifts', 'reports.tab.sales', 'reports.tab.gameplay', 'reports.tab.cash', 'reports.tab.operatorActions',
    'reports.range.today', 'reports.range.7d', 'reports.range.30d', 'reports.range.from', 'reports.range.to',
    'reports.sum.gross', 'reports.sum.refunds', 'reports.sum.net',
    'reports.sum.duration', 'reports.sum.package', 'reports.sum.bonus', 'reports.sum.revenue',
    'reports.sum.cashIn', 'reports.sum.cashOut', 'reports.sum.netCash', 'reports.sum.actions',
    'reports.col.state', 'reports.col.opened', 'reports.col.closed', 'reports.col.movements',
    'reports.col.expectedCash', 'reports.col.countedCash', 'reports.col.difference',
    'reports.col.total', 'reports.col.paid', 'reports.col.refund', 'reports.col.lines', 'reports.col.qty',
    'reports.col.created', 'reports.col.paidAt', 'reports.col.seat', 'reports.col.device',
    'reports.col.playerKind', 'reports.col.duration', 'reports.col.revenue',
    'reports.col.source', 'reports.col.opType', 'reports.col.impact', 'reports.col.reason',
    'reports.col.operator', 'reports.col.action', 'reports.col.outcome', 'reports.col.count',
    'reports.col.first', 'reports.col.last'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the journal keys', () => {
  for (const key of [
    'journal.noAccess', 'journal.empty', 'journal.limitNote',
    'journal.filter.action', 'journal.filter.outcome', 'journal.filter.targetType',
    'journal.filter.apply', 'journal.filter.reset',
    'journal.outcome.all', 'journal.outcome.succeeded', 'journal.outcome.denied',
    'journal.col.date', 'journal.col.actor', 'journal.col.action', 'journal.col.target',
    'journal.col.outcome', 'journal.col.source', 'journal.col.details', 'journal.actor.system'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run to verify failure.**

Run: `npm test -- messages` (cwd `src/AFK4.Platform.Web`)
Expected: FAIL (keys missing / type error on unknown keys).

- [ ] **Step 3: Add keys to the `ru` object** (place after the existing `clientPackages.*` block):

```ts
    // reports
    'reports.noAccess': 'Нет доступа к отчётам.',
    'reports.empty': 'Нет данных за выбранный период.',
    'reports.limitNote': 'Показаны последние записи за период (ограничение сервера).',
    'reports.export': 'Экспорт CSV',
    'reports.export.error': 'Не удалось выгрузить CSV',
    'reports.tab.shifts': 'Смены',
    'reports.tab.sales': 'Продажи',
    'reports.tab.gameplay': 'Игровое время',
    'reports.tab.cash': 'Касса',
    'reports.tab.operatorActions': 'Действия операторов',
    'reports.range.today': 'Сегодня',
    'reports.range.7d': '7 дней',
    'reports.range.30d': '30 дней',
    'reports.range.from': 'С',
    'reports.range.to': 'По',
    'reports.sum.gross': 'Продажи (брутто)',
    'reports.sum.refunds': 'Возвраты',
    'reports.sum.net': 'Продажи (нетто)',
    'reports.sum.duration': 'Время, мин',
    'reports.sum.package': 'Пакетные, мин',
    'reports.sum.bonus': 'Бонусные, мин',
    'reports.sum.revenue': 'Выручка',
    'reports.sum.cashIn': 'Приход',
    'reports.sum.cashOut': 'Расход',
    'reports.sum.netCash': 'Нетто по кассе',
    'reports.sum.actions': 'Всего действий',
    'reports.col.state': 'Состояние',
    'reports.col.opened': 'Открыта',
    'reports.col.closed': 'Закрыта',
    'reports.col.movements': 'Движения',
    'reports.col.expectedCash': 'Ожидаемая касса',
    'reports.col.countedCash': 'Посчитано',
    'reports.col.difference': 'Расхождение',
    'reports.col.total': 'Сумма',
    'reports.col.paid': 'Оплачено',
    'reports.col.refund': 'Возврат',
    'reports.col.lines': 'Позиции',
    'reports.col.qty': 'Кол-во',
    'reports.col.created': 'Создано',
    'reports.col.paidAt': 'Оплачено в',
    'reports.col.seat': 'Место',
    'reports.col.device': 'Устройство',
    'reports.col.playerKind': 'Тип игрока',
    'reports.col.duration': 'Длительность, мин',
    'reports.col.revenue': 'Выручка',
    'reports.col.source': 'Источник',
    'reports.col.opType': 'Операция',
    'reports.col.impact': 'Влияние на кассу',
    'reports.col.reason': 'Причина',
    'reports.col.operator': 'Оператор',
    'reports.col.action': 'Действие',
    'reports.col.outcome': 'Исход',
    'reports.col.count': 'Кол-во',
    'reports.col.first': 'Первое',
    'reports.col.last': 'Последнее',
    // journal
    'journal.noAccess': 'Нет доступа к журналу.',
    'journal.empty': 'Нет записей за выбранный период.',
    'journal.limitNote': 'Показаны последние записи за период (ограничение сервера).',
    'journal.filter.action': 'Действие',
    'journal.filter.outcome': 'Исход',
    'journal.filter.targetType': 'Тип объекта',
    'journal.filter.apply': 'Применить',
    'journal.filter.reset': 'Сбросить',
    'journal.outcome.all': 'Все исходы',
    'journal.outcome.succeeded': 'Успех',
    'journal.outcome.denied': 'Отказано',
    'journal.col.date': 'Дата',
    'journal.col.actor': 'Актор',
    'journal.col.action': 'Действие',
    'journal.col.target': 'Объект',
    'journal.col.outcome': 'Исход',
    'journal.col.source': 'Источник',
    'journal.col.details': 'Детали',
    'journal.actor.system': 'Система',
```

- [ ] **Step 4: Add the same keys to the `en` object** (after its `clientPackages.*` block), English values:

```ts
    // reports
    'reports.noAccess': 'No access to reports.',
    'reports.empty': 'No data for the selected period.',
    'reports.limitNote': 'Showing the latest records for the period (server limit).',
    'reports.export': 'Export CSV',
    'reports.export.error': 'Failed to export CSV',
    'reports.tab.shifts': 'Shifts',
    'reports.tab.sales': 'Sales',
    'reports.tab.gameplay': 'Gameplay time',
    'reports.tab.cash': 'Cash',
    'reports.tab.operatorActions': 'Operator actions',
    'reports.range.today': 'Today',
    'reports.range.7d': '7 days',
    'reports.range.30d': '30 days',
    'reports.range.from': 'From',
    'reports.range.to': 'To',
    'reports.sum.gross': 'Gross sales',
    'reports.sum.refunds': 'Refunds',
    'reports.sum.net': 'Net sales',
    'reports.sum.duration': 'Time, min',
    'reports.sum.package': 'Package, min',
    'reports.sum.bonus': 'Bonus, min',
    'reports.sum.revenue': 'Revenue',
    'reports.sum.cashIn': 'Cash in',
    'reports.sum.cashOut': 'Cash out',
    'reports.sum.netCash': 'Net cash',
    'reports.sum.actions': 'Total actions',
    'reports.col.state': 'State',
    'reports.col.opened': 'Opened',
    'reports.col.closed': 'Closed',
    'reports.col.movements': 'Movements',
    'reports.col.expectedCash': 'Expected cash',
    'reports.col.countedCash': 'Counted',
    'reports.col.difference': 'Difference',
    'reports.col.total': 'Total',
    'reports.col.paid': 'Paid',
    'reports.col.refund': 'Refund',
    'reports.col.lines': 'Lines',
    'reports.col.qty': 'Qty',
    'reports.col.created': 'Created',
    'reports.col.paidAt': 'Paid at',
    'reports.col.seat': 'Seat',
    'reports.col.device': 'Device',
    'reports.col.playerKind': 'Player kind',
    'reports.col.duration': 'Duration, min',
    'reports.col.revenue': 'Revenue',
    'reports.col.source': 'Source',
    'reports.col.opType': 'Operation',
    'reports.col.impact': 'Cash impact',
    'reports.col.reason': 'Reason',
    'reports.col.operator': 'Operator',
    'reports.col.action': 'Action',
    'reports.col.outcome': 'Outcome',
    'reports.col.count': 'Count',
    'reports.col.first': 'First',
    'reports.col.last': 'Last',
    // journal
    'journal.noAccess': 'No access to the journal.',
    'journal.empty': 'No records for the selected period.',
    'journal.limitNote': 'Showing the latest records for the period (server limit).',
    'journal.filter.action': 'Action',
    'journal.filter.outcome': 'Outcome',
    'journal.filter.targetType': 'Target type',
    'journal.filter.apply': 'Apply',
    'journal.filter.reset': 'Reset',
    'journal.outcome.all': 'All outcomes',
    'journal.outcome.succeeded': 'Succeeded',
    'journal.outcome.denied': 'Denied',
    'journal.col.date': 'Date',
    'journal.col.actor': 'Actor',
    'journal.col.action': 'Action',
    'journal.col.target': 'Target',
    'journal.col.outcome': 'Outcome',
    'journal.col.source': 'Source',
    'journal.col.details': 'Details',
    'journal.actor.system': 'System',
```

- [ ] **Step 5: Run tests.** Run: `npm test -- messages` → PASS (parity + both coverage blocks).
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): i18n keys for reports + journal"`

---

### Task 2: Report + audit types

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/types.ts` (append near the existing report/money types; `MoneyMinor` already exists — do NOT redefine it)

- [ ] **Step 1: Append the interfaces.**

```ts
// --- Reports (block 7a) ---
export interface ShiftReportRow {
  shiftId: string;
  organizationId: string;
  branchId: string;
  openedByStaffUserId: string;
  closedByStaffUserId: string | null;
  state: string;
  startingCash: MoneyMinor;
  cashMovementsTotal: MoneyMinor;
  posCashPaymentsTotal: MoneyMinor;
  posRefundsTotal: MoneyMinor;
  billingCashImpactTotal: MoneyMinor;
  expectedCash: MoneyMinor;
  countedCash: MoneyMinor | null;
  difference: MoneyMinor | null;
  openedAtUtc: string;
  closedAtUtc: string | null;
}
export interface ShiftReport { rows: ShiftReportRow[]; limit: number; }

export interface SalesReportRow {
  posSaleId: string;
  organizationId: string;
  branchId: string;
  shiftId: string;
  createdByStaffUserId: string;
  state: string;
  total: MoneyMinor;
  paidAmount: MoneyMinor;
  refundAmount: MoneyMinor;
  lineCount: number;
  itemQuantity: number;
  createdAtUtc: string;
  paidAtUtc: string | null;
  refundedAtUtc: string | null;
  voidedAtUtc: string | null;
}
export interface SalesReport {
  rows: SalesReportRow[];
  limit: number;
  grossSalesTotal: MoneyMinor;
  refundsTotal: MoneyMinor;
  netSalesTotal: MoneyMinor;
}

export interface GameplayTimeReportRow {
  sessionId: string;
  organizationId: string;
  branchId: string;
  seatId: string;
  deviceId: string;
  createdByStaffUserId: string;
  playerKind: string;
  playerAccountId: string | null;
  state: string;
  durationSeconds: number;
  packageSeconds: number;
  bonusSeconds: number;
  gameplayRevenue: MoneyMinor;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  endsAtUtc: string | null;
}
export interface GameplayTimeReport {
  rows: GameplayTimeReportRow[];
  limit: number;
  totalDurationSeconds: number;
  totalPackageSeconds: number;
  totalBonusSeconds: number;
  gameplayRevenueTotal: MoneyMinor;
}

export interface CashOperationReportRow {
  operationId: string;
  organizationId: string;
  branchId: string;
  shiftId: string | null;
  createdByStaffUserId: string;
  sourceType: string;
  operationType: string;
  cashImpact: MoneyMinor;
  reason: string;
  createdAtUtc: string;
}
export interface CashOperationReport {
  rows: CashOperationReportRow[];
  limit: number;
  cashInTotal: MoneyMinor;
  cashOutTotal: MoneyMinor;
  netCashTotal: MoneyMinor;
}

export interface OperatorActionReportRow {
  actorStaffUserId: string | null;
  actorDisplayName: string;
  action: string;
  outcome: string;
  count: number;
  firstAtUtc: string;
  lastAtUtc: string;
}
export interface OperatorActionReport {
  rows: OperatorActionReportRow[];
  limit: number;
  totalActionCount: number;
}

// --- Audit (block 7a) ---
export interface AuditRecord {
  auditRecordId: string;
  organizationId: string;
  branchId: string | null;
  actorStaffUserId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
  actorPlatformAdminUserId: string | null;
}
export interface AuditSearchResult { records: AuditRecord[]; limit: number; }
export interface AuditSearchQuery {
  action?: string;
  outcome?: string;
  targetType?: string;
  fromUtc?: string;
  toUtc?: string;
  limit?: number;
}
```

- [ ] **Step 2: Verify it compiles.** Run: `npm run build` → no type errors. (No runtime test for type-only changes.)
- [ ] **Step 3: Commit.** `git add -A && git commit -m "feat(club): report + audit DTO types"`

---

### Task 3: clubApi wrappers (5 reports + CSV + audit)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/clubApi.ts`

- [ ] **Step 1: Add the type imports** to the existing `@/api/types` import block in `clubApi.ts`:

```ts
  ShiftReport,
  SalesReport,
  GameplayTimeReport,
  CashOperationReport,
  OperatorActionReport,
  AuditSearchResult,
  AuditSearchQuery,
```

- [ ] **Step 2: Add the public methods** (place after `getDashboardSummaryForRange`):

```ts
  public getShiftReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<ShiftReport> {
    return this.send<ShiftReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/shifts${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getSalesReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<SalesReport> {
    return this.send<SalesReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/sales${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getGameplayTimeReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<GameplayTimeReport> {
    return this.send<GameplayTimeReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/gameplay-time${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getCashOperationReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<CashOperationReport> {
    return this.send<CashOperationReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/cash-operations${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public getOperatorActionReport(branchId: string, fromUtc?: string, toUtc?: string, limit?: number): Promise<OperatorActionReport> {
    return this.send<OperatorActionReport>('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/operator-actions${reportQuery(fromUtc, toUtc, limit)}`);
  }

  public async fetchReportCsv(branchId: string, name: string, fromUtc?: string, toUtc?: string): Promise<Blob> {
    const response = await this.sendRaw('GET', `/api/branches/${encodeURIComponent(branchId)}/reports/${name}/export.csv${reportQuery(fromUtc, toUtc, undefined)}`);
    return response.blob();
  }

  public searchAudit(branchId: string, query: AuditSearchQuery): Promise<AuditSearchResult> {
    const params = new URLSearchParams();
    if (query.action !== undefined && query.action.length > 0) params.set('action', query.action);
    if (query.outcome !== undefined && query.outcome.length > 0) params.set('outcome', query.outcome);
    if (query.targetType !== undefined && query.targetType.length > 0) params.set('targetType', query.targetType);
    if (query.fromUtc !== undefined) params.set('fromUtc', query.fromUtc);
    if (query.toUtc !== undefined) params.set('toUtc', query.toUtc);
    if (query.limit !== undefined) params.set('limit', String(query.limit));
    const qs = params.toString();
    return this.send<AuditSearchResult>('GET', `/api/branches/${encodeURIComponent(branchId)}/audit${qs.length > 0 ? `?${qs}` : ''}`);
  }
```

- [ ] **Step 3: Add the module-level helper** at the BOTTOM of `clubApi.ts` (outside the class), near any other module-local helpers:

```ts
function reportQuery(fromUtc?: string, toUtc?: string, limit?: number): string {
  const params = new URLSearchParams();
  if (fromUtc !== undefined) params.set('fromUtc', fromUtc);
  if (toUtc !== undefined) params.set('toUtc', toUtc);
  if (limit !== undefined) params.set('limit', String(limit));
  const qs = params.toString();
  return qs.length > 0 ? `?${qs}` : '';
}
```

- [ ] **Step 4: Verify build.** Run: `npm run build` → clean. (Thin wrappers; exercised via hook/component tests downstream — no per-wrapper unit test, matching existing GET wrappers.)
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): report + audit clubApi wrappers"`

---

### Task 4: saveBlob download util

**Files:**
- Create: `src/AFK4.Platform.Web/src/lib/saveBlob.ts`
- Test: `src/AFK4.Platform.Web/src/lib/saveBlob.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect, vi, afterEach } from 'vitest';
import { saveBlob } from './saveBlob';

afterEach(() => { vi.restoreAllMocks(); });

it('creates an object URL, clicks an anchor with the filename, and revokes', () => {
  const createObjectURL = vi.fn(() => 'blob:test');
  const revokeObjectURL = vi.fn();
  (URL as unknown as { createObjectURL: typeof createObjectURL }).createObjectURL = createObjectURL;
  (URL as unknown as { revokeObjectURL: typeof revokeObjectURL }).revokeObjectURL = revokeObjectURL;
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

  saveBlob(new Blob(['a,b,c']), 'report.csv');

  expect(createObjectURL).toHaveBeenCalledTimes(1);
  expect(click).toHaveBeenCalledTimes(1);
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:test');
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- saveBlob` → FAIL (module not found).

- [ ] **Step 3: Implement.**

```ts
/** Triggers a browser download of a Blob under the given filename. */
export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
```

- [ ] **Step 4: Run.** Run: `npm test -- saveBlob` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): saveBlob download util"`

---

### Task 5: reportsModel (presets, formatters, 5 builders)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/reportsModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/reports/reportsModel.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect } from 'vitest';
import type {
  ShiftReport, SalesReport, GameplayTimeReport, CashOperationReport, OperatorActionReport
} from '@/api/types';
import {
  presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc,
  buildShiftReport, buildSalesReport, buildGameplayReport, buildCashReport, buildOperatorActionReport,
  type ReportFormatters
} from './reportsModel';

const fmt: ReportFormatters = {
  formatCurrency: (a, c) => `${a.toFixed(2)} ${c}`,
  formatNumber: n => String(n),
  formatDate: iso => iso.slice(0, 10)
};

it('presetRange today spans the full UTC day', () => {
  const r = presetRange('today', new Date('2026-05-30T15:00:00.000Z'));
  expect(r.fromUtc).toBe('2026-05-30T00:00:00.000Z');
  expect(r.toUtc).toBe('2026-05-30T23:59:59.000Z');
});

it('presetRange 7d starts six days earlier', () => {
  const r = presetRange('7d', new Date('2026-05-30T15:00:00.000Z'));
  expect(r.fromUtc).toBe('2026-05-24T00:00:00.000Z');
});

it('date input helpers round-trip', () => {
  expect(isoToDateInput('2026-05-30T23:59:59.000Z')).toBe('2026-05-30');
  expect(dateInputToFromUtc('2026-05-30')).toBe('2026-05-30T00:00:00.000Z');
  expect(dateInputToToUtc('2026-05-30')).toBe('2026-05-30T23:59:59.000Z');
});

it('buildSalesReport produces summary cards and formatted rows', () => {
  const report: SalesReport = {
    limit: 100,
    grossSalesTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    refundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
    netSalesTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    rows: [{
      posSaleId: 's1', organizationId: 'o', branchId: 'b', shiftId: 'sh', createdByStaffUserId: 'u',
      state: 'Paid', total: { currencyCode: 'RUB', minorUnits: 150000 },
      paidAmount: { currencyCode: 'RUB', minorUnits: 150000 }, refundAmount: { currencyCode: 'RUB', minorUnits: 0 },
      lineCount: 2, itemQuantity: 3, createdAtUtc: '2026-05-30T10:00:00.000Z',
      paidAtUtc: '2026-05-30T10:01:00.000Z', refundedAtUtc: null, voidedAtUtc: null
    }]
  };
  const view = buildSalesReport(report, fmt);
  expect(view.summaryCards).toEqual([
    { labelKey: 'reports.sum.gross', value: '1500.00 RUB' },
    { labelKey: 'reports.sum.refunds', value: '0.00 RUB' },
    { labelKey: 'reports.sum.net', value: '1500.00 RUB' }
  ]);
  expect(view.rows[0].total).toBe('1500.00 RUB');
  expect(view.rows[0].qty).toBe('3');
});

it('buildGameplayReport converts seconds to minutes', () => {
  const report: GameplayTimeReport = {
    limit: 100, totalDurationSeconds: 3600, totalPackageSeconds: 1800, totalBonusSeconds: 600,
    gameplayRevenueTotal: { currencyCode: 'RUB', minorUnits: 50000 },
    rows: [{
      sessionId: 'g1', organizationId: 'o', branchId: 'b', seatId: 'seat', deviceId: 'dev',
      createdByStaffUserId: 'u', playerKind: 'Member', playerAccountId: 'p', state: 'Ended',
      durationSeconds: 3600, packageSeconds: 1800, bonusSeconds: 600,
      gameplayRevenue: { currencyCode: 'RUB', minorUnits: 50000 },
      startedAtUtc: '2026-05-30T09:00:00.000Z', endedAtUtc: '2026-05-30T10:00:00.000Z', endsAtUtc: null
    }]
  };
  const view = buildGameplayReport(report, fmt);
  expect(view.summaryCards[0]).toEqual({ labelKey: 'reports.sum.duration', value: '60' });
  expect(view.rows[0].duration).toBe('60');
});

it('buildShiftReport has no summary cards and renders optional money as dash', () => {
  const report: ShiftReport = {
    limit: 100,
    rows: [{
      shiftId: 'sh1', organizationId: 'o', branchId: 'b', openedByStaffUserId: 'u', closedByStaffUserId: null,
      state: 'Open', startingCash: { currencyCode: 'RUB', minorUnits: 0 },
      cashMovementsTotal: { currencyCode: 'RUB', minorUnits: 1000 },
      posCashPaymentsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      posRefundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      billingCashImpactTotal: { currencyCode: 'RUB', minorUnits: 0 },
      expectedCash: { currencyCode: 'RUB', minorUnits: 1000 },
      countedCash: null, difference: null,
      openedAtUtc: '2026-05-30T08:00:00.000Z', closedAtUtc: null
    }]
  };
  const view = buildShiftReport(report, fmt);
  expect(view.summaryCards).toEqual([]);
  expect(view.rows[0].counted).toBe('—');
  expect(view.rows[0].closed).toBe('—');
});

it('buildCashReport and buildOperatorActionReport build totals', () => {
  const cash: CashOperationReport = {
    limit: 100,
    cashInTotal: { currencyCode: 'RUB', minorUnits: 200000 },
    cashOutTotal: { currencyCode: 'RUB', minorUnits: 50000 },
    netCashTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    rows: [{
      operationId: 'c1', organizationId: 'o', branchId: 'b', shiftId: null, createdByStaffUserId: 'u',
      sourceType: 'Shift', operationType: 'Deposit', cashImpact: { currencyCode: 'RUB', minorUnits: 200000 },
      reason: 'open', createdAtUtc: '2026-05-30T08:00:00.000Z'
    }]
  };
  expect(buildCashReport(cash, fmt).summaryCards[2]).toEqual({ labelKey: 'reports.sum.netCash', value: '1500.00 RUB' });

  const actions: OperatorActionReport = {
    limit: 100, totalActionCount: 42,
    rows: [{
      actorStaffUserId: 'u1', actorDisplayName: 'Иван', action: 'session.start', outcome: 'Succeeded',
      count: 42, firstAtUtc: '2026-05-30T08:00:00.000Z', lastAtUtc: '2026-05-30T20:00:00.000Z'
    }]
  };
  const view = buildOperatorActionReport(actions, fmt);
  expect(view.summaryCards).toEqual([{ labelKey: 'reports.sum.actions', value: '42' }]);
  expect(view.rows[0].operator).toBe('Иван');
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- reportsModel` → FAIL (module not found).

- [ ] **Step 3: Implement `reportsModel.ts`.**

```ts
import type { MessageKey } from '@/i18n/messages';
import type {
  MoneyMinor, ShiftReport, SalesReport, GameplayTimeReport,
  CashOperationReport, OperatorActionReport
} from '@/api/types';
import { minorToMajor } from '../money';

export interface ReportFormatters {
  formatCurrency: (amountMajor: number, currencyCode: string) => string;
  formatNumber: (value: number) => string;
  formatDate: (iso: string) => string;
}

export interface SummaryCard { labelKey: MessageKey; value: string; }
export interface ReportColumn { key: string; labelKey: MessageKey; }
export interface ReportView {
  summaryCards: SummaryCard[];
  columns: ReportColumn[];
  rows: Record<string, string>[];
}

export interface DateRange { fromUtc: string; toUtc: string; }
export type RangePreset = 'today' | '7d' | '30d';

export function presetRange(preset: RangePreset, now: Date): DateRange {
  const y = now.getUTCFullYear();
  const m = now.getUTCMonth();
  const d = now.getUTCDate();
  const back = preset === 'today' ? 0 : preset === '7d' ? 6 : 29;
  const start = new Date(Date.UTC(y, m, d - back, 0, 0, 0));
  const end = new Date(Date.UTC(y, m, d, 23, 59, 59));
  return { fromUtc: start.toISOString(), toUtc: end.toISOString() };
}

export function isoToDateInput(iso: string): string { return iso.slice(0, 10); }
export function dateInputToFromUtc(date: string): string { return `${date}T00:00:00.000Z`; }
export function dateInputToToUtc(date: string): string { return `${date}T23:59:59.000Z`; }

function money(m: MoneyMinor, fmt: ReportFormatters): string {
  return fmt.formatCurrency(minorToMajor(m.minorUnits), m.currencyCode);
}
function optMoney(m: MoneyMinor | null, fmt: ReportFormatters): string {
  return m === null ? '—' : money(m, fmt);
}
function minutes(seconds: number, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(seconds / 60));
}
function optDate(iso: string | null, fmt: ReportFormatters): string {
  return iso === null ? '—' : fmt.formatDate(iso);
}

export function buildShiftReport(report: ShiftReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [],
    columns: [
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'opened', labelKey: 'reports.col.opened' },
      { key: 'closed', labelKey: 'reports.col.closed' },
      { key: 'movements', labelKey: 'reports.col.movements' },
      { key: 'expected', labelKey: 'reports.col.expectedCash' },
      { key: 'counted', labelKey: 'reports.col.countedCash' },
      { key: 'difference', labelKey: 'reports.col.difference' }
    ],
    rows: report.rows.map(r => ({
      state: r.state,
      opened: fmt.formatDate(r.openedAtUtc),
      closed: optDate(r.closedAtUtc, fmt),
      movements: money(r.cashMovementsTotal, fmt),
      expected: money(r.expectedCash, fmt),
      counted: optMoney(r.countedCash, fmt),
      difference: optMoney(r.difference, fmt)
    }))
  };
}

export function buildSalesReport(report: SalesReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.gross', value: money(report.grossSalesTotal, fmt) },
      { labelKey: 'reports.sum.refunds', value: money(report.refundsTotal, fmt) },
      { labelKey: 'reports.sum.net', value: money(report.netSalesTotal, fmt) }
    ],
    columns: [
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'total', labelKey: 'reports.col.total' },
      { key: 'paid', labelKey: 'reports.col.paid' },
      { key: 'refund', labelKey: 'reports.col.refund' },
      { key: 'lines', labelKey: 'reports.col.lines' },
      { key: 'qty', labelKey: 'reports.col.qty' },
      { key: 'created', labelKey: 'reports.col.created' },
      { key: 'paidAt', labelKey: 'reports.col.paidAt' }
    ],
    rows: report.rows.map(r => ({
      state: r.state,
      total: money(r.total, fmt),
      paid: money(r.paidAmount, fmt),
      refund: money(r.refundAmount, fmt),
      lines: fmt.formatNumber(r.lineCount),
      qty: fmt.formatNumber(r.itemQuantity),
      created: fmt.formatDate(r.createdAtUtc),
      paidAt: optDate(r.paidAtUtc, fmt)
    }))
  };
}

export function buildGameplayReport(report: GameplayTimeReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.duration', value: minutes(report.totalDurationSeconds, fmt) },
      { labelKey: 'reports.sum.package', value: minutes(report.totalPackageSeconds, fmt) },
      { labelKey: 'reports.sum.bonus', value: minutes(report.totalBonusSeconds, fmt) },
      { labelKey: 'reports.sum.revenue', value: money(report.gameplayRevenueTotal, fmt) }
    ],
    columns: [
      { key: 'seat', labelKey: 'reports.col.seat' },
      { key: 'device', labelKey: 'reports.col.device' },
      { key: 'playerKind', labelKey: 'reports.col.playerKind' },
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'duration', labelKey: 'reports.col.duration' },
      { key: 'revenue', labelKey: 'reports.col.revenue' }
    ],
    rows: report.rows.map(r => ({
      seat: r.seatId,
      device: r.deviceId,
      playerKind: r.playerKind,
      state: r.state,
      duration: minutes(r.durationSeconds, fmt),
      revenue: money(r.gameplayRevenue, fmt)
    }))
  };
}

export function buildCashReport(report: CashOperationReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.cashIn', value: money(report.cashInTotal, fmt) },
      { labelKey: 'reports.sum.cashOut', value: money(report.cashOutTotal, fmt) },
      { labelKey: 'reports.sum.netCash', value: money(report.netCashTotal, fmt) }
    ],
    columns: [
      { key: 'source', labelKey: 'reports.col.source' },
      { key: 'opType', labelKey: 'reports.col.opType' },
      { key: 'impact', labelKey: 'reports.col.impact' },
      { key: 'reason', labelKey: 'reports.col.reason' },
      { key: 'created', labelKey: 'reports.col.created' }
    ],
    rows: report.rows.map(r => ({
      source: r.sourceType,
      opType: r.operationType,
      impact: money(r.cashImpact, fmt),
      reason: r.reason,
      created: fmt.formatDate(r.createdAtUtc)
    }))
  };
}

export function buildOperatorActionReport(report: OperatorActionReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.actions', value: fmt.formatNumber(report.totalActionCount) }
    ],
    columns: [
      { key: 'operator', labelKey: 'reports.col.operator' },
      { key: 'action', labelKey: 'reports.col.action' },
      { key: 'outcome', labelKey: 'reports.col.outcome' },
      { key: 'count', labelKey: 'reports.col.count' },
      { key: 'first', labelKey: 'reports.col.first' },
      { key: 'last', labelKey: 'reports.col.last' }
    ],
    rows: report.rows.map(r => ({
      operator: r.actorDisplayName,
      action: r.action,
      outcome: r.outcome,
      count: fmt.formatNumber(r.count),
      first: fmt.formatDate(r.firstAtUtc),
      last: fmt.formatDate(r.lastAtUtc)
    }))
  };
}
```

> Note: `minutes` is used by `buildGameplayReport`; do not remove it as "unused" — `tsc` will confirm.

- [ ] **Step 4: Run.** Run: `npm test -- reportsModel` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): reportsModel builders + date-range helpers"`

---

### Task 6: useReport generic hook

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/useReport.ts`
- Test: `src/AFK4.Platform.Web/src/club/reports/useReport.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useReport } from './useReport';

it('loads data into the ready state', async () => {
  const loader = vi.fn<() => Promise<{ n: number }>>(async () => ({ n: 7 }));
  const { result } = renderHook(() => useReport(loader, ['k']));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.data.n).toBe(7);
});

it('reports an error when the load fails', async () => {
  const loader = vi.fn<() => Promise<{ n: number }>>(async () => { throw new Error('boom'); });
  const { result } = renderHook(() => useReport(loader, ['k']));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- useReport` → FAIL.

- [ ] **Step 3: Implement.**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';

export type ReportState<T> =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; data: T; retry: () => void };

export function useReport<T>(loader: () => Promise<T>, deps: readonly unknown[]): ReportState<T> {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [data, setData] = useState<T | null>(null);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    loaderRef.current()
      .then(result => { if (!cancelled) { setData(result); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || data === null) return { status: 'loading' };
  return { status: 'ready', data, retry };
}
```

> Error-check is placed BEFORE the loading/null check (lesson from 6a `useWalletSummary`).

- [ ] **Step 4: Run.** Run: `npm test -- useReport` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): generic useReport hook"`

---

### Task 7: DateRangeControl

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/DateRangeControl.tsx`
- Test: `src/AFK4.Platform.Web/src/club/reports/DateRangeControl.test.tsx`

- [ ] **Step 1: Write the failing test.**

```ts
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { DateRangeControl } from './DateRangeControl';
import type { DateRange } from './reportsModel';

const range: DateRange = { fromUtc: '2026-05-30T00:00:00.000Z', toUtc: '2026-05-30T23:59:59.000Z' };

it('emits a new range when the from-date input changes', () => {
  const onChange = vi.fn();
  render(<I18nProvider><DateRangeControl value={range} onChange={onChange} /></I18nProvider>);
  fireEvent.change(screen.getByLabelText('С'), { target: { value: '2026-05-01' } });
  expect(onChange).toHaveBeenCalledWith({
    fromUtc: '2026-05-01T00:00:00.000Z',
    toUtc: '2026-05-30T23:59:59.000Z'
  });
});

it('emits a preset range when a preset button is clicked', () => {
  const onChange = vi.fn();
  render(<I18nProvider><DateRangeControl value={range} onChange={onChange} /></I18nProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Сегодня' }));
  expect(onChange).toHaveBeenCalledTimes(1);
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- DateRangeControl` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useI18n } from '@/i18n/I18nProvider';
import {
  isoToDateInput, dateInputToFromUtc, dateInputToToUtc, presetRange,
  type DateRange, type RangePreset
} from './reportsModel';

const PRESETS: { preset: RangePreset; labelKey: 'reports.range.today' | 'reports.range.7d' | 'reports.range.30d' }[] = [
  { preset: 'today', labelKey: 'reports.range.today' },
  { preset: '7d', labelKey: 'reports.range.7d' },
  { preset: '30d', labelKey: 'reports.range.30d' }
];

export function DateRangeControl({ value, onChange }: { value: DateRange; onChange: (range: DateRange) => void }) {
  const { t } = useI18n();
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex gap-2">
        {PRESETS.map(p => (
          <Button key={p.preset} variant="outline" size="sm"
            onClick={() => onChange(presetRange(p.preset, new Date()))}>
            {t(p.labelKey)}
          </Button>
        ))}
      </div>
      <label className="flex flex-col gap-1 text-xs text-muted-foreground">
        {t('reports.range.from')}
        <Input type="date" aria-label={t('reports.range.from')} value={isoToDateInput(value.fromUtc)}
          onChange={e => onChange({ fromUtc: dateInputToFromUtc(e.target.value), toUtc: value.toUtc })} />
      </label>
      <label className="flex flex-col gap-1 text-xs text-muted-foreground">
        {t('reports.range.to')}
        <Input type="date" aria-label={t('reports.range.to')} value={isoToDateInput(value.toUtc)}
          onChange={e => onChange({ fromUtc: value.fromUtc, toUtc: dateInputToToUtc(e.target.value) })} />
      </label>
    </div>
  );
}
```

- [ ] **Step 4: Run.** Run: `npm test -- DateRangeControl` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): DateRangeControl"`

---

### Task 8: ExportButton

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/ExportButton.tsx`
- Test: `src/AFK4.Platform.Web/src/club/reports/ExportButton.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ExportButton } from './ExportButton';
import * as saveBlobModule from '@/lib/saveBlob';

it('calls onExport then saveBlob with the filename', async () => {
  const blob = new Blob(['x']);
  const onExport = vi.fn<() => Promise<Blob>>(async () => blob);
  const save = vi.spyOn(saveBlobModule, 'saveBlob').mockImplementation(() => {});
  render(
    <I18nProvider><ToastProvider>
      <ExportButton onExport={onExport} filename="sales.csv" />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Экспорт CSV' }));
  await waitFor(() => expect(onExport).toHaveBeenCalled());
  await waitFor(() => expect(save).toHaveBeenCalledWith(blob, 'sales.csv'));
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- ExportButton` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { saveBlob } from '@/lib/saveBlob';

export function ExportButton({ onExport, filename }: { onExport: () => Promise<Blob>; filename: string }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  async function run() {
    setBusy(true);
    try {
      const blob = await onExport();
      saveBlob(blob, filename);
    } catch {
      toast({ title: t('reports.export.error'), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Button variant="outline" size="sm" disabled={busy} onClick={() => void run()}>
      {t('reports.export')}
    </Button>
  );
}
```

- [ ] **Step 4: Run.** Run: `npm test -- ExportButton` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): ExportButton"`

---

### Task 9: ReportTab (generic report renderer)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/ReportTab.tsx`
- Test: `src/AFK4.Platform.Web/src/club/reports/ReportTab.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ReportTab } from './ReportTab';
import type { ReportView, ReportFormatters } from './reportsModel';

function build(_data: { ok: boolean }, _fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [{ labelKey: 'reports.sum.net', value: '10.00 RUB' }],
    columns: [{ key: 'state', labelKey: 'reports.col.state' }],
    rows: [{ state: 'Paid' }]
  };
}

it('renders summary cards and table rows from the built view', async () => {
  const load = vi.fn<() => Promise<{ ok: boolean }>>(async () => ({ ok: true }));
  render(
    <I18nProvider><ToastProvider>
      <ReportTab load={load} build={build} deps={['k']} onExport={async () => new Blob()} filename="x.csv" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('Paid')).toBeInTheDocument();
  expect(screen.getByText('10.00 RUB')).toBeInTheDocument();
});

it('shows the empty state when there are no rows', async () => {
  const load = vi.fn<() => Promise<{ ok: boolean }>>(async () => ({ ok: true }));
  const emptyBuild = (): ReportView => ({ summaryCards: [], columns: [{ key: 'state', labelKey: 'reports.col.state' }], rows: [] });
  render(
    <I18nProvider><ToastProvider>
      <ReportTab load={load} build={emptyBuild} deps={['k']} onExport={async () => new Blob()} filename="x.csv" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('Нет данных за выбранный период.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- ReportTab` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { useReport } from './useReport';
import { ExportButton } from './ExportButton';
import type { ReportView, ReportFormatters } from './reportsModel';

export function ReportTab<T>({ load, build, deps, onExport, filename }: {
  load: () => Promise<T>;
  build: (data: T, fmt: ReportFormatters) => ReportView;
  deps: readonly unknown[];
  onExport: () => Promise<Blob>;
  filename: string;
}) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useReport(load, deps);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const view = build(state.data, { formatCurrency, formatNumber, formatDate });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <ExportButton onExport={onExport} filename={filename} />
      </div>

      {view.summaryCards.length > 0 && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {view.summaryCards.map(card => (
            <Card key={card.labelKey}>
              <CardContent className="p-4">
                <div className="text-xs text-muted-foreground">{t(card.labelKey)}</div>
                <div className="mt-1 text-lg font-semibold tabular-nums">{card.value}</div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {view.rows.length === 0 ? (
        <EmptyState message={t('reports.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              {view.columns.map(col => <TableHead key={col.key}>{t(col.labelKey)}</TableHead>)}
            </TableRow>
          </TableHeader>
          <TableBody>
            {view.rows.map((row, index) => (
              <TableRow key={index}>
                {view.columns.map(col => <TableCell key={col.key} className="tabular-nums">{row[col.key]}</TableCell>)}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('reports.limitNote')}</p>
    </div>
  );
}
```

> `state.error` / `state.retry` use existing `state.error`/`state.retry` message keys already present in the catalog (used by every other screen). If `tsc` flags them as unknown keys, they exist — confirm via `messages.ts`.

- [ ] **Step 4: Run.** Run: `npm test -- ReportTab` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): generic ReportTab"`

---

### Task 10: ReportsScreen shell

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/reports/ReportsScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/reports/ReportsScreen.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ReportsScreen } from './ReportsScreen';

function fakeClient() {
  return {
    getShiftReport: vi.fn<() => Promise<object>>(async () => ({ rows: [], limit: 100 })),
    getSalesReport: vi.fn<() => Promise<object>>(async () => ({
      rows: [], limit: 100,
      grossSalesTotal: { currencyCode: 'RUB', minorUnits: 0 },
      refundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      netSalesTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getGameplayTimeReport: vi.fn<() => Promise<object>>(async () => ({
      rows: [], limit: 100, totalDurationSeconds: 0, totalPackageSeconds: 0, totalBonusSeconds: 0,
      gameplayRevenueTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getCashOperationReport: vi.fn<() => Promise<object>>(async () => ({
      rows: [], limit: 100,
      cashInTotal: { currencyCode: 'RUB', minorUnits: 0 },
      cashOutTotal: { currencyCode: 'RUB', minorUnits: 0 },
      netCashTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getOperatorActionReport: vi.fn<() => Promise<object>>(async () => ({ rows: [], limit: 100, totalActionCount: 0 })),
    fetchReportCsv: vi.fn<() => Promise<Blob>>(async () => new Blob())
  };
}

it('loads the default (shifts) tab', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <ReportsScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(client.getShiftReport).toHaveBeenCalled());
});

it('switches to the sales tab and loads it', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <ReportsScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  const tab = screen.getByRole('tab', { name: 'Продажи' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  await waitFor(() => expect(client.getSalesReport).toHaveBeenCalled());
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- ReportsScreen` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { DateRangeControl } from './DateRangeControl';
import { ReportTab } from './ReportTab';
import {
  presetRange, isoToDateInput, type DateRange,
  buildShiftReport, buildSalesReport, buildGameplayReport, buildCashReport, buildOperatorActionReport
} from './reportsModel';

type Client = Pick<ClubApiClient,
  'getShiftReport' | 'getSalesReport' | 'getGameplayTimeReport' | 'getCashOperationReport' | 'getOperatorActionReport' | 'fetchReportCsv'>;

export function ReportsScreen({ client, branchId }: { client: Client; branchId: string }) {
  const { t } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const deps = [branchId, range.fromUtc, range.toUtc] as const;
  const suffix = isoToDateInput(range.fromUtc);

  return (
    <div className="flex flex-col gap-4">
      <DateRangeControl value={range} onChange={setRange} />
      <Tabs defaultValue="shifts">
        <TabsList>
          <TabsTrigger value="shifts">{t('reports.tab.shifts')}</TabsTrigger>
          <TabsTrigger value="sales">{t('reports.tab.sales')}</TabsTrigger>
          <TabsTrigger value="gameplay">{t('reports.tab.gameplay')}</TabsTrigger>
          <TabsTrigger value="cash">{t('reports.tab.cash')}</TabsTrigger>
          <TabsTrigger value="operatorActions">{t('reports.tab.operatorActions')}</TabsTrigger>
        </TabsList>
        <TabsContent value="shifts">
          <ReportTab
            load={() => client.getShiftReport(branchId, range.fromUtc, range.toUtc)}
            build={buildShiftReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'shifts', range.fromUtc, range.toUtc)}
            filename={`shifts-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="sales">
          <ReportTab
            load={() => client.getSalesReport(branchId, range.fromUtc, range.toUtc)}
            build={buildSalesReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'sales', range.fromUtc, range.toUtc)}
            filename={`sales-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="gameplay">
          <ReportTab
            load={() => client.getGameplayTimeReport(branchId, range.fromUtc, range.toUtc)}
            build={buildGameplayReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'gameplay-time', range.fromUtc, range.toUtc)}
            filename={`gameplay-time-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="cash">
          <ReportTab
            load={() => client.getCashOperationReport(branchId, range.fromUtc, range.toUtc)}
            build={buildCashReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'cash-operations', range.fromUtc, range.toUtc)}
            filename={`cash-operations-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="operatorActions">
          <ReportTab
            load={() => client.getOperatorActionReport(branchId, range.fromUtc, range.toUtc)}
            build={buildOperatorActionReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'operator-actions', range.fromUtc, range.toUtc)}
            filename={`operator-actions-${suffix}.csv`} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 4: Run.** Run: `npm test -- ReportsScreen` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): ReportsScreen shell"`

---

### Task 11: Reports route + nav wiring

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

- [ ] **Step 1: Enable the reports nav item.** In `nav.ts`, change the `reports` item `soon: true` → `soon: false`:

```ts
      { key: 'reports', labelKey: 'nav.reports', path: '/club/reports', ownerOnly: false, soon: false },
```

- [ ] **Step 2: Add the route kind.** In `App.tsx`, add to the `ClubRoute` union (after `clubMonetization`):

```ts
  | { kind: 'clubReports' }
```

- [ ] **Step 3: Add the screen title.** In `CLUB_SCREEN_TITLE`:

```ts
  clubReports: 'Отчёты',
```

- [ ] **Step 4: Add to `pathForRoute`** (in the `switch`):

```ts
    case 'clubReports':
      return '/club/reports';
```

- [ ] **Step 5: Add to `resolvePlatformRoute`** (next to the other `/club/*` checks):

```ts
    if (path === '/club/reports') {
      return { route: { kind: 'clubReports' } };
    }
```

- [ ] **Step 6: Add to `isClubRoute`:**

```ts
    || route.kind === 'clubReports'
```

- [ ] **Step 7: Add the import + render branch in `ClubArea`.** Import near the other club screen imports:

```ts
import { ReportsScreen } from './club/reports/ReportsScreen';
```

Add a render branch BEFORE the final `else → LegacyClubScreen` (e.g., after the `clubSettings` branch):

```tsx
      ) : route.kind === 'clubReports' ? (
        session.permissions.includes('reports.view') ? (
          <ReportsScreen client={clubClient} branchId={activeBranchId} />
        ) : (
          <EmptyState message={t('reports.noAccess')} />
        )
```

- [ ] **Step 8: Build + full suite.** Run: `npm run build` → clean. Run: `npm test` → all green.
- [ ] **Step 9: Commit.** `git add -A && git commit -m "feat(club): wire reports route + nav"`

---

### Task 12: auditModel

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/journal/auditModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/journal/auditModel.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect } from 'vitest';
import type { AuditSearchResult } from '@/api/types';
import { toAuditRows, outcomeBadgeVariant } from './auditModel';

const result: AuditSearchResult = {
  limit: 100,
  records: [
    {
      auditRecordId: 'a1', organizationId: 'o', branchId: 'b', actorStaffUserId: 'staff-1',
      action: 'session.start', targetType: 'Session', targetId: 'sess-9', outcome: 'Succeeded',
      sourceApp: 'operator', detailsJson: '{"k":1}', createdAtUtc: '2026-05-30T10:00:00.000Z',
      actorPlatformAdminUserId: null
    },
    {
      auditRecordId: 'a2', organizationId: 'o', branchId: null, actorStaffUserId: null,
      action: 'login', targetType: 'Staff', targetId: null, outcome: 'Denied',
      sourceApp: 'web', detailsJson: '{}', createdAtUtc: '2026-05-30T11:00:00.000Z',
      actorPlatformAdminUserId: null
    }
  ]
};

it('builds rows with resolved actor and target', () => {
  const rows = toAuditRows(result, { formatDate: iso => iso.slice(0, 10) }, 'Система');
  expect(rows[0].actor).toBe('staff-1');
  expect(rows[0].target).toBe('Session (sess-9)');
  expect(rows[0].date).toBe('2026-05-30');
  expect(rows[1].actor).toBe('Система');
  expect(rows[1].target).toBe('Staff');
});

it('maps outcomes to badge variants', () => {
  expect(outcomeBadgeVariant('Succeeded')).toBe('secondary');
  expect(outcomeBadgeVariant('Denied')).toBe('destructive');
  expect(outcomeBadgeVariant('Other')).toBe('outline');
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- auditModel` → FAIL.

- [ ] **Step 3: Implement.**

```ts
import type { AuditSearchResult } from '@/api/types';

export type OutcomeVariant = 'secondary' | 'destructive' | 'outline';

export interface AuditRow {
  id: string;
  date: string;
  actor: string;
  action: string;
  target: string;
  outcome: string;
  outcomeVariant: OutcomeVariant;
  source: string;
  details: string;
}

export function outcomeBadgeVariant(outcome: string): OutcomeVariant {
  if (outcome === 'Succeeded') return 'secondary';
  if (outcome === 'Denied') return 'destructive';
  return 'outline';
}

export function toAuditRows(
  result: AuditSearchResult,
  fmt: { formatDate: (iso: string) => string },
  systemLabel: string
): AuditRow[] {
  return result.records.map(record => ({
    id: record.auditRecordId,
    date: fmt.formatDate(record.createdAtUtc),
    actor: record.actorStaffUserId ?? record.actorPlatformAdminUserId ?? systemLabel,
    action: record.action,
    target: record.targetId === null ? record.targetType : `${record.targetType} (${record.targetId})`,
    outcome: record.outcome,
    outcomeVariant: outcomeBadgeVariant(record.outcome),
    source: record.sourceApp,
    details: record.detailsJson
  }));
}
```

- [ ] **Step 4: Run.** Run: `npm test -- auditModel` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): auditModel"`

---

### Task 13: useAudit hook

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/journal/useAudit.ts`
- Test: `src/AFK4.Platform.Web/src/club/journal/useAudit.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { AuditSearchResult } from '@/api/types';
import { useAudit } from './useAudit';

const result: AuditSearchResult = { limit: 100, records: [] };

it('loads audit records into the ready state', async () => {
  const client = { searchAudit: vi.fn<() => Promise<AuditSearchResult>>(async () => result) };
  const { result: hook } = renderHook(() => useAudit(client as never, 'b1', { limit: 100 }));
  await waitFor(() => expect(hook.current.status).toBe('ready'));
  expect(client.searchAudit).toHaveBeenCalledWith('b1', { limit: 100 });
});

it('reports an error when the load fails', async () => {
  const client = { searchAudit: vi.fn<() => Promise<AuditSearchResult>>(async () => { throw new Error('boom'); }) };
  const { result: hook } = renderHook(() => useAudit(client as never, 'b1', { limit: 100 }));
  await waitFor(() => expect(hook.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- useAudit` → FAIL.

- [ ] **Step 3: Implement.**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import type { AuditSearchQuery, AuditRecord } from '@/api/types';

type Loadable = Pick<ClubApiClient, 'searchAudit'>;

export type AuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: AuditRecord[]; retry: () => void };

export function useAudit(client: Loadable, branchId: string, query: AuditSearchQuery): AuditState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [records, setRecords] = useState<AuditRecord[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);
  const queryKey = JSON.stringify(query);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.searchAudit(branchId, query)
      .then(result => { if (!cancelled) { setRecords(result.records); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
```

> Load-only (no `useI18n` in the hook — keeps it provider-free and testable like every other `use*` hook). Row formatting happens in `JournalScreen` via `toAuditRows`.

- [ ] **Step 4: Run.** Run: `npm test -- useAudit` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): useAudit hook"`

---

### Task 14: AuditFilters

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/journal/AuditFilters.tsx`
- Test: `src/AFK4.Platform.Web/src/club/journal/AuditFilters.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AuditFilters } from './AuditFilters';
import { presetRange, type DateRange } from '../reports/reportsModel';

const range: DateRange = presetRange('today', new Date('2026-05-30T12:00:00.000Z'));

it('applies the typed action filter', () => {
  const onApply = vi.fn();
  render(<I18nProvider>
    <AuditFilters range={range} onRangeChange={() => {}} onApply={onApply} onReset={() => {}} />
  </I18nProvider>);
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'login' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  expect(onApply).toHaveBeenCalledWith(expect.objectContaining({ action: 'login' }));
});

it('resets the draft', () => {
  const onReset = vi.fn();
  render(<I18nProvider>
    <AuditFilters range={range} onRangeChange={() => {}} onApply={() => {}} onReset={onReset} />
  </I18nProvider>);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить' }));
  expect(onReset).toHaveBeenCalled();
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- AuditFilters` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useI18n } from '@/i18n/I18nProvider';
import { DateRangeControl } from '../reports/DateRangeControl';
import type { DateRange } from '../reports/reportsModel';

export interface AuditDraft { action: string; outcome: string; targetType: string; }

export function AuditFilters({ range, onRangeChange, onApply, onReset }: {
  range: DateRange;
  onRangeChange: (range: DateRange) => void;
  onApply: (draft: AuditDraft) => void;
  onReset: () => void;
}) {
  const { t } = useI18n();
  const [action, setAction] = useState('');
  const [outcome, setOutcome] = useState('all');
  const [targetType, setTargetType] = useState('');

  function reset() {
    setAction('');
    setOutcome('all');
    setTargetType('');
    onReset();
  }

  return (
    <div className="flex flex-col gap-3">
      <DateRangeControl value={range} onChange={onRangeChange} />
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.action')}
          <Input aria-label={t('journal.filter.action')} value={action} onChange={e => setAction(e.target.value)} />
        </label>
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.targetType')}
          <Input aria-label={t('journal.filter.targetType')} value={targetType} onChange={e => setTargetType(e.target.value)} />
        </label>
        <div className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('journal.filter.outcome')}
          <Select value={outcome} onValueChange={setOutcome}>
            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t('journal.outcome.all')}</SelectItem>
              <SelectItem value="Succeeded">{t('journal.outcome.succeeded')}</SelectItem>
              <SelectItem value="Denied">{t('journal.outcome.denied')}</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <Button onClick={() => onApply({ action, outcome, targetType })}>{t('journal.filter.apply')}</Button>
        <Button variant="outline" onClick={reset}>{t('journal.filter.reset')}</Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run.** Run: `npm test -- AuditFilters` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): AuditFilters"`

---

### Task 15: JournalScreen

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/journal/JournalScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/journal/JournalScreen.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { AuditSearchResult } from '@/api/types';
import { JournalScreen } from './JournalScreen';

const record = {
  auditRecordId: 'a1', organizationId: 'o', branchId: 'b', actorStaffUserId: 'staff-1',
  action: 'session.start', targetType: 'Session', targetId: 'sess-9', outcome: 'Succeeded',
  sourceApp: 'operator', detailsJson: '{}', createdAtUtc: '2026-05-30T10:00:00.000Z',
  actorPlatformAdminUserId: null
};

function fakeClient() {
  return { searchAudit: vi.fn<() => Promise<AuditSearchResult>>(async () => ({ limit: 100, records: [record] })) };
}

it('renders audit rows', async () => {
  render(
    <I18nProvider><ToastProvider>
      <JournalScreen client={fakeClient() as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('session.start')).toBeInTheDocument();
});

it('refetches with the applied action filter', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <JournalScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  await screen.findByText('session.start');
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'login' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  await waitFor(() =>
    expect(client.searchAudit.mock.calls.some(c => (c[1] as { action?: string }).action === 'login')).toBe(true)
  );
});
```

- [ ] **Step 2: Run to verify failure.** Run: `npm test -- JournalScreen` → FAIL.

- [ ] **Step 3: Implement.**

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { AuditSearchQuery } from '@/api/types';
import { AuditFilters, type AuditDraft } from './AuditFilters';
import { useAudit } from './useAudit';
import { toAuditRows } from './auditModel';
import { presetRange, type DateRange } from '../reports/reportsModel';

type Client = Pick<ClubApiClient, 'searchAudit'>;

const DEFAULT_LIMIT = 100;

function buildQuery(range: DateRange, draft: AuditDraft): AuditSearchQuery {
  const query: AuditSearchQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) query.action = draft.action;
  if (draft.targetType.length > 0) query.targetType = draft.targetType;
  if (draft.outcome !== 'all') query.outcome = draft.outcome;
  return query;
}

export function JournalScreen({ client, branchId }: { client: Client; branchId: string }) {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<AuditSearchQuery>(() => buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' }));
  const state = useAudit(client, branchId, query);

  function handleRangeChange(next: DateRange) {
    setRange(next);
    setQuery(prev => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  return (
    <div className="flex flex-col gap-4">
      <AuditFilters
        range={range}
        onRangeChange={handleRangeChange}
        onApply={draft => setQuery(buildQuery(range, draft))}
        onReset={() => setQuery(buildQuery(range, { action: '', outcome: 'all', targetType: '' }))}
      />

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <JournalTable
          rows={toAuditRows({ records: state.records, limit: 0 }, { formatDate }, t('journal.actor.system'))}
        />
      )}

      <p className="text-xs text-muted-foreground">{t('journal.limitNote')}</p>
    </div>
  );
}

function JournalTable({ rows }: { rows: ReturnType<typeof toAuditRows> }) {
  const { t } = useI18n();
  if (rows.length === 0) return <EmptyState message={t('journal.empty')} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('journal.col.date')}</TableHead>
          <TableHead>{t('journal.col.actor')}</TableHead>
          <TableHead>{t('journal.col.action')}</TableHead>
          <TableHead>{t('journal.col.target')}</TableHead>
          <TableHead>{t('journal.col.outcome')}</TableHead>
          <TableHead>{t('journal.col.source')}</TableHead>
          <TableHead>{t('journal.col.details')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.id}>
            <TableCell className="tabular-nums">{row.date}</TableCell>
            <TableCell>{row.actor}</TableCell>
            <TableCell className="font-medium">{row.action}</TableCell>
            <TableCell>{row.target}</TableCell>
            <TableCell><Badge variant={row.outcomeVariant}>{row.outcome}</Badge></TableCell>
            <TableCell>{row.source}</TableCell>
            <TableCell className="max-w-xs truncate font-mono text-xs" title={row.details}>{row.details}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
```

- [ ] **Step 4: Run.** Run: `npm test -- JournalScreen` → PASS.
- [ ] **Step 5: Commit.** `git add -A && git commit -m "feat(club): JournalScreen"`

---

### Task 16: Journal route + nav wiring + final gate

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts` (add `nav.journal` to ru + en)
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

- [ ] **Step 1: Add the nav label.** In `messages.ts` ru, after `'nav.reports'`: `'nav.journal': 'Журнал',`. In en: `'nav.journal': 'Journal',`.

- [ ] **Step 2: Add the nav item.** In `nav.ts`, in the `branch` group items, after `reports`:

```ts
      { key: 'journal', labelKey: 'nav.journal', path: '/club/journal', ownerOnly: false, soon: false },
```

- [ ] **Step 3: Add the route kind** to `ClubRoute` (after `clubReports`):

```ts
  | { kind: 'clubJournal' }
```

- [ ] **Step 4: Screen title.** In `CLUB_SCREEN_TITLE`: `clubJournal: 'Журнал',`

- [ ] **Step 5: pathForRoute:**

```ts
    case 'clubJournal':
      return '/club/journal';
```

- [ ] **Step 6: resolvePlatformRoute:**

```ts
    if (path === '/club/journal') {
      return { route: { kind: 'clubJournal' } };
    }
```

- [ ] **Step 7: isClubRoute:** `|| route.kind === 'clubJournal'`

- [ ] **Step 8: Import + render branch in `ClubArea`** (after the `clubReports` branch):

```ts
import { JournalScreen } from './club/journal/JournalScreen';
```

```tsx
      ) : route.kind === 'clubJournal' ? (
        session.permissions.includes('audit.view') ? (
          <JournalScreen client={clubClient} branchId={activeBranchId} />
        ) : (
          <EmptyState message={t('journal.noAccess')} />
        )
```

- [ ] **Step 9: Add `nav.journal` to the messages parity test.** In `messages.test.ts`, extend the journal coverage block's key array with `'nav.journal'`.

- [ ] **Step 10: Final gate.** Run: `npm test` → ALL green. Run: `npm run build` (`tsc -b && vite build`) → clean.
- [ ] **Step 11: Commit.** `git add -A && git commit -m "feat(club): wire journal route + nav; finish 7a"`

---

## Self-Review notes (for the controller)
- **Pick-expansion:** `ReportsScreen`/`JournalScreen` `client` Picks are the only consumers; `ClubArea` passes the full `clubClient`, so no downstream Pick ripple (unlike clients screens).
- **`activeBranchId`:** passed to both screens exactly as `VenueScreen`/`ClientsScreen` receive it; no null-guard added (matches existing screens).
- **`state.error`/`state.retry` keys:** already in the catalog (used everywhere) — not added by this plan.
- **CSV `fetchReportCsv`:** thin wrapper, no dedicated unit test; covered via `ExportButton` (mocked) and exercised in `ReportsScreen`.
- **Dead branch-detail routes / Install / Profile:** untouched here — they belong to Plan 7b.
