# Оператор — слияние дублей (под-проект №3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Закрыть два оставшихся пробела оператора vs Platform.Web/club (историческая отчётность + журнал аудита филиала) чисто на фронте, превратив секцию «Отчёты» в destination-switcher, и убрать мёртвый воркспейс `logs`.

**Architecture:** Секция «Отчёты» (`WorkspaceId 'dashboard'`) из одиночного экрана становится destination-switcher `ReportsWorkspace` (паттерн `NetworkWorkspace`: `management-layout` + `management-nav` + `management-active-pane`) с тремя destination — Обзор (перенос тела `DashboardWorkspace`), История (4 отчёта), Журнал (аудит филиала, переиспользование UI из №2). Бэкенд НЕ меняется — все эндпоинты и права уже есть. Мёртвый `logs` удаляется. Финал — делегированный аудит паритета дублей.

**Tech Stack:** React/TS в `AFK4.Operator.App.Web`; тесты `bun test` (happy-dom + jest-dom); сборка `bun run build` = `tsc -b && vite build` (тайпчекает и тест-файлы); i18n `@afk4/i18n` (генерация `bun run gen` из `locales/{ru,en,tg}.json`); деньги `@afk4/money` (`minorToMajor`).

## Global Constraints

- **Бэкенд/права/контракты НЕ меняются.** Все эндпоинты (`/api/branches/{id}/reports/*`, `/export.csv`, `/api/branches/{id}/audit`) и права (`reports.view`, `audit.view`) уже существуют.
- **Feature-shape:** каждый экран = каталог `<feature>/` с `*Destination.tsx` + `use*.ts` + `*Model.ts`; логика отделена от рендера.
- **Атомы оператора, не shadcn:** таблицы через `table-panel`/`ctable-head`/`ctable-row`, кнопки `ui-btn`/`ui-btn--primary`, чипы `ui-chip ui-chip--status is-*`, состояния через `EmptyState`/`Skeleton` из `operatorPrimitives`, обёртка экрана — `ManagementScreen`. Никаких hardcoded строк — только i18n `t('op.reports.*')`.
- **Отчётные DTO нетипизированы** (`ReportResultDto = Record<string, unknown>`): читать поля через `readArray`/`readString`/`readNumber`/`readMoney`/`readRecord` из `operatorHelpers.ts`, деньги форматировать `formatMinorUnits`.
- **i18n honesty (#37):** новые ключи во всех трёх локалях (ru/en/tg) с реальным таджикским; гвард `messages.test.ts` — идентичные наборы ключей + `tg !== ru` (кроме allowlist-заимствований). Регенерация `bun run gen`.
- **Без AI-подписей** в коммитах.
- Каждая задача заканчивается зелёными `bun test` (затронутые тесты) и — где меняются публичные типы/удаляется код — `bun run build`.

**Рабочая директория фронта:** `src/AFK4.Operator.App.Web` (все относительные пути ниже — от `src/AFK4.Operator.App.Web/src`, если не указано иное). Команды `bun`/`bun run` запускать из `src/AFK4.Operator.App.Web`.

---

## File Structure

**Создаются:**
- `src/reports/reportsNav.ts` — реестр destination секции (overview/history/journal) + `allowedReportsDestinations`.
- `src/reports/ReportsWorkspace.tsx` — switcher-хост секции.
- `src/reports/overview/OverviewDestination.tsx` — тонкая обёртка над телом дашборда (перенос).
- `src/reports/history/reportModel.ts` — маппинг `ReportResultDto` → `ReportView` для 4 отчётов.
- `src/reports/history/reportTypes.ts` — декларативный реестр 4 отчётов (load/build/exportCsv/labels).
- `src/reports/history/useReport.ts` — общий хук загрузки отчёта.
- `src/reports/history/ReportTable.tsx` — рендер `ReportView` на атомах оператора + CSV-кнопка.
- `src/reports/history/HistoryDestination.tsx` — под-таб-полоса 4 отчётов.
- `src/reports/journal/useBranchAudit.ts` — хук загрузки аудита филиала.
- `src/reports/journal/BranchJournalDestination.tsx` — экран журнала филиала (переиспользует UI №2).
- Тесты: `reports/reportsNav.test.ts`, `reports/history/reportModel.test.ts`, `reports/history/HistoryDestination.test.tsx`, `reports/journal/BranchJournalDestination.test.tsx`.

**Модифицируются:**
- `src/DashboardWorkspace.tsx` — корень `<main>` → `<section>` (embedded), тело выносится/переиспользуется в `OverviewDestination`.
- `src/WorkspaceRouter.tsx` — `dashboard` рендерит `<ReportsWorkspace>`; удаляется ветка/импорт `logs`.
- `src/operatorPermissions.ts` — `workspacePermissionRules.dashboard` += `audit.view`; удаление `'logs'` из `workspaceIds` и правил.
- `src/operatorTypes.ts` — удаление `'logs'` из `WorkspaceId`.
- `src/operatorData.ts` — (лейбл секции «Отчёты» уже есть; правок обычно не требует — проверить).
- `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.ts` (регенерация) + `src/messages.test.ts` (allowlist/presence, если нужно).
- `test/operatorVisibility.test.ts` — секция «Отчёты» видна при `reports.view` ИЛИ `audit.view`; убрать `'logs'` из ожиданий.

**Удаляются:**
- `src/BackendLogsWorkspace.tsx` (+ его тесты, если есть).

---

## Референс-файлы (читать перед реализацией)

- Шаблон switcher: `src/network/NetworkWorkspace.tsx`, `src/network/networkNav.ts`.
- Журнал №2 для переиспользования: `src/network/journal/{OrgAuditFilters.tsx,dateRange.ts,orgAuditModel.ts,useOrgAudit.ts,JournalDestination.tsx}`.
- Отчётные методы клиента: `src/api/clients/shifts.ts` (`getShiftReport`/`getCashOperationReport`/`getGameplayTimeReport`/`getOperatorActionReport` + `export*ReportCsv`).
- Аудит-клиент: `src/api/clients/audit.ts` (`search({branchId, action, outcome, targetType, fromUtc, toUtc, limit})` → `Record<string, unknown>`).
- Модель колонок 1:1 источник: `src/AFK4.Platform.Web/src/club/reports/reportsModel.ts` (типизированная — у нас нетипизированная через `read*`).
- Хелперы чтения/формата/скачивания: `src/operatorHelpers.ts` (`readArray`/`readString`/`readNumber`/`readMoney`/`readRecord`/`formatMinorUnits`/`downloadTextFile`/`createAuthenticatedOperatorClients`).
- Дашборд для переноса: `src/DashboardWorkspace.tsx` (корень `<main className="workspace-screen dashboard-screen">`).

---

## Task 1: Реестр destination «Отчёты» (`reportsNav.ts`)

**Files:**
- Create: `src/reports/reportsNav.ts`
- Test: `src/reports/reportsNav.test.ts`

**Interfaces:**
- Consumes: `permissionNames` (`viewReports = 'reports.view'`, `viewAudit = 'audit.view'`), `hasAnyPermission` из `operatorPermissions`, `OperatorAuthSession` из `authClient`, `MessageKey`/`LucideIcon`.
- Produces:
  - `type ReportsDestinationId = 'overview' | 'history' | 'journal'`
  - `interface ReportsDestination { id: ReportsDestinationId; labelKey: MessageKey; subtitleKey: MessageKey; Icon: LucideIcon; permissions: readonly string[] }`
  - `const reportsDestinations: readonly ReportsDestination[]`
  - `function allowedReportsDestinations(session: OperatorAuthSession | null): ReportsDestination[]`

- [ ] **Step 1: Написать падающий тест**

Создать `src/reports/reportsNav.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { allowedReportsDestinations, reportsDestinations } from './reportsNav';
import { permissionNames } from '../permissionNames';

function session(permissions: string[]) {
  return { permissions } as never;
}

describe('reportsNav', () => {
  it('lists overview/history/journal in order', () => {
    expect(reportsDestinations.map((d) => d.id)).toEqual(['overview', 'history', 'journal']);
  });

  it('shows overview+history for reports.view, hides journal', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports])).map((d) => d.id);
    expect(ids).toEqual(['overview', 'history']);
  });

  it('shows only journal for audit.view alone', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['journal']);
  });

  it('shows all three when both permissions present', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports, permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['overview', 'history', 'journal']);
  });

  it('hides section entirely with no relevant permission', () => {
    expect(allowedReportsDestinations(session([])).length).toBe(0);
  });
});
```

- [ ] **Step 2: Запустить — упасть**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/reportsNav.test.ts`
Expected: FAIL — модуль `./reportsNav` не найден.

- [ ] **Step 3: Реализовать `reportsNav.ts`**

```ts
import type { LucideIcon } from 'lucide-react';
import { Gauge, History, ScrollText } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ReportsDestinationId = 'overview' | 'history' | 'journal';

export interface ReportsDestination {
  id: ReportsDestinationId;
  labelKey: MessageKey;
  subtitleKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // видим, если у сессии есть ЛЮБОЕ из
}

export const reportsDestinations: readonly ReportsDestination[] = [
  {
    id: 'overview',
    labelKey: 'op.reports.dest.overview',
    subtitleKey: 'op.reports.dest.overview.subtitle',
    Icon: Gauge,
    permissions: [permissionNames.viewReports]
  },
  {
    id: 'history',
    labelKey: 'op.reports.dest.history',
    subtitleKey: 'op.reports.dest.history.subtitle',
    Icon: History,
    permissions: [permissionNames.viewReports]
  },
  {
    id: 'journal',
    labelKey: 'op.reports.dest.journal',
    subtitleKey: 'op.reports.dest.journal.subtitle',
    Icon: ScrollText,
    permissions: [permissionNames.viewAudit]
  }
];

export function allowedReportsDestinations(session: OperatorAuthSession | null): ReportsDestination[] {
  return reportsDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
```

> Проверить, что `permissionNames.viewReports` и `permissionNames.viewAudit` существуют (`grep -n "viewReports\|viewAudit" src/permissionNames.ts`). Оба уже используются в `workspacePermissionRules` — существуют.

- [ ] **Step 4: Запустить — пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/reportsNav.test.ts`
Expected: PASS (5 тестов).

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/reports/reportsNav.ts src/AFK4.Operator.App.Web/src/reports/reportsNav.test.ts
git commit -m "feat(operator): реестр destination секции «Отчёты» (overview/history/journal)"
```

---

## Task 2: Модель отчётов (`reportModel.ts`)

**Files:**
- Create: `src/reports/history/reportModel.ts`
- Test: `src/reports/history/reportModel.test.ts`

**Interfaces:**
- Consumes: `readArray`/`readString`/`readNumber`/`readMoney` из `operatorHelpers`, `formatMinorUnits` из `operatorHelpers`, `MessageKey`.
- Produces:
  - `interface ReportFormatters { formatMinorUnits: (minorUnits: number, currencyCode: string) => string; formatNumber: (n: number) => string; formatDate: (iso: string) => string; }`
  - `interface SummaryCard { labelKey: MessageKey; value: string }`
  - `interface ReportColumn { key: string; labelKey: MessageKey }`
  - `interface ReportView { summaryCards: SummaryCard[]; columns: ReportColumn[]; rows: Record<string, string>[] }`
  - `buildShiftReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView`
  - `buildCashOperationReportView(result, fmt): ReportView`
  - `buildGameplayTimeReportView(result, fmt): ReportView`
  - `buildOperatorActionReportView(result, fmt): ReportView`

**Замечание по чтению DTO:** `readMoney(record, key)` возвращает `{ minorUnits: number; currencyCode: string } | null` (см. `operatorHelpers.ts` — сверить сигнатуру перед реализацией; дашборд использует `readMoney`). `readArray(record, key)` → `unknown[]`; каждую строку привести к записи через `readRecord`-подобный доступ. Ниже — хелперы внутри модуля.

- [ ] **Step 1: Написать падающий тест**

Создать `src/reports/history/reportModel.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import {
  buildShiftReportView,
  buildCashOperationReportView,
  buildGameplayTimeReportView,
  buildOperatorActionReportView,
  type ReportFormatters
} from './reportModel';

const fmt: ReportFormatters = {
  formatMinorUnits: (minor, code) => `${(minor / 100).toFixed(2)} ${code}`,
  formatNumber: (n) => String(n),
  formatDate: (iso) => iso.slice(0, 10)
};

const money = (minorUnits: number) => ({ minorUnits, currencyCode: 'TJS' });

describe('reportModel', () => {
  it('maps a shift report row incl. null counted/difference to «—»', () => {
    const view = buildShiftReportView({
      rows: [{
        state: 'Closed',
        openedAtUtc: '2026-07-01T08:00:00Z',
        closedAtUtc: null,
        cashMovementsTotal: money(5000),
        expectedCash: money(12000),
        countedCash: null,
        difference: null
      }]
    }, fmt);
    expect(view.columns.map((c) => c.key)).toEqual(['state', 'opened', 'closed', 'movements', 'expected', 'counted', 'difference']);
    expect(view.rows[0]).toMatchObject({
      state: 'Closed',
      opened: '2026-07-01',
      closed: '—',
      movements: '50.00 TJS',
      expected: '120.00 TJS',
      counted: '—',
      difference: '—'
    });
  });

  it('maps cash-operation summary + rows', () => {
    const view = buildCashOperationReportView({
      cashInTotal: money(30000),
      cashOutTotal: money(10000),
      netCashTotal: money(20000),
      rows: [{ sourceType: 'Shift', operationType: 'CashIn', cashImpact: money(30000), reason: 'float', createdAtUtc: '2026-07-01T09:00:00Z' }]
    }, fmt);
    expect(view.summaryCards.map((c) => c.value)).toEqual(['300.00 TJS', '100.00 TJS', '200.00 TJS']);
    expect(view.rows[0]).toMatchObject({ source: 'Shift', opType: 'CashIn', impact: '300.00 TJS', reason: 'float', created: '2026-07-01' });
  });

  it('maps gameplay-time seconds → minutes', () => {
    const view = buildGameplayTimeReportView({
      totalDurationSeconds: 3600,
      totalPackageSeconds: 1800,
      totalBonusSeconds: 0,
      gameplayRevenueTotal: money(45000),
      rows: [{ seatId: 'A1', deviceId: 'PC-1', playerKind: 'Guest', state: 'Ended', durationSeconds: 3600, gameplayRevenue: money(45000) }]
    }, fmt);
    expect(view.summaryCards[0].value).toBe('60'); // 3600s → 60 min
    expect(view.rows[0]).toMatchObject({ seat: 'A1', device: 'PC-1', playerKind: 'Guest', state: 'Ended', duration: '60', revenue: '450.00 TJS' });
  });

  it('maps operator-action counts', () => {
    const view = buildOperatorActionReportView({
      totalActionCount: 7,
      rows: [{ actorDisplayName: 'Иван', action: 'sale.pay', outcome: 'Succeeded', count: 5, firstAtUtc: '2026-07-01T09:00:00Z', lastAtUtc: '2026-07-01T18:00:00Z' }]
    }, fmt);
    expect(view.summaryCards[0].value).toBe('7');
    expect(view.rows[0]).toMatchObject({ operator: 'Иван', action: 'sale.pay', outcome: 'Succeeded', count: '5', first: '2026-07-01', last: '2026-07-01' });
  });

  it('empty rows → empty view rows', () => {
    expect(buildShiftReportView({ rows: [] }, fmt).rows).toEqual([]);
    expect(buildShiftReportView({}, fmt).rows).toEqual([]);
  });
});
```

- [ ] **Step 2: Запустить — упасть**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/history/reportModel.test.ts`
Expected: FAIL — модуль не найден.

- [ ] **Step 3: Реализовать `reportModel.ts`**

Сигнатуры хелперов (сверено в `operatorHelpers.ts`): `readString(value, name, fallback=''): string` — читает `value[name]` как строку; `readNumber(value, name, fallback=0): number`; `readArray<T=unknown>(value, name): T[]` — читает `value[name]` как массив; `readMoney(value, name): { currencyCode: string; minorUnits: number } | null` — читает `value[name]` как деньги. Каждый элемент `rows` — уже объект-запись, поэтому берём `readArray<Record<string, unknown>>(result, 'rows')` и читаем поля прямо из строки (`readRecord` НЕ нужен — он извлекает вложенный объект по ключу, а не приводит сам элемент).

```ts
import type { MessageKey } from '@afk4/i18n';
import { readArray, readMoney, readNumber, readString } from '../../operatorHelpers';

export interface ReportFormatters {
  formatMinorUnits: (minorUnits: number, currencyCode: string) => string;
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

function rowsOf(result: Record<string, unknown>): Record<string, unknown>[] {
  return readArray<Record<string, unknown>>(result, 'rows');
}

function money(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  const m = readMoney(rec, key);
  return m === null ? '—' : fmt.formatMinorUnits(m.minorUnits, m.currencyCode);
}

function dateOrDash(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  const iso = readString(rec, key);
  return iso === '' ? '—' : fmt.formatDate(iso);
}

function minutes(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(readNumber(rec, key) / 60));
}

function totalMinutes(result: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(readNumber(result, key) / 60));
}

export function buildShiftReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [],
    columns: [
      { key: 'state', labelKey: 'op.reports.col.state' },
      { key: 'opened', labelKey: 'op.reports.col.opened' },
      { key: 'closed', labelKey: 'op.reports.col.closed' },
      { key: 'movements', labelKey: 'op.reports.col.movements' },
      { key: 'expected', labelKey: 'op.reports.col.expectedCash' },
      { key: 'counted', labelKey: 'op.reports.col.countedCash' },
      { key: 'difference', labelKey: 'op.reports.col.difference' }
    ],
    rows: rowsOf(result).map((r) => ({
      state: readString(r, 'state'),
      opened: dateOrDash(r, 'openedAtUtc', fmt),
      closed: dateOrDash(r, 'closedAtUtc', fmt),
      movements: money(r, 'cashMovementsTotal', fmt),
      expected: money(r, 'expectedCash', fmt),
      counted: money(r, 'countedCash', fmt),
      difference: money(r, 'difference', fmt)
    }))
  };
}

export function buildCashOperationReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.cashIn', value: money(result, 'cashInTotal', fmt) },
      { labelKey: 'op.reports.sum.cashOut', value: money(result, 'cashOutTotal', fmt) },
      { labelKey: 'op.reports.sum.netCash', value: money(result, 'netCashTotal', fmt) }
    ],
    columns: [
      { key: 'source', labelKey: 'op.reports.col.source' },
      { key: 'opType', labelKey: 'op.reports.col.opType' },
      { key: 'impact', labelKey: 'op.reports.col.impact' },
      { key: 'reason', labelKey: 'op.reports.col.reason' },
      { key: 'created', labelKey: 'op.reports.col.created' }
    ],
    rows: rowsOf(result).map((r) => ({
      source: readString(r, 'sourceType'),
      opType: readString(r, 'operationType'),
      impact: money(r, 'cashImpact', fmt),
      reason: readString(r, 'reason'),
      created: dateOrDash(r, 'createdAtUtc', fmt)
    }))
  };
}

export function buildGameplayTimeReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.duration', value: totalMinutes(result, 'totalDurationSeconds', fmt) },
      { labelKey: 'op.reports.sum.package', value: totalMinutes(result, 'totalPackageSeconds', fmt) },
      { labelKey: 'op.reports.sum.bonus', value: totalMinutes(result, 'totalBonusSeconds', fmt) },
      { labelKey: 'op.reports.sum.revenue', value: money(result, 'gameplayRevenueTotal', fmt) }
    ],
    columns: [
      { key: 'seat', labelKey: 'op.reports.col.seat' },
      { key: 'device', labelKey: 'op.reports.col.device' },
      { key: 'playerKind', labelKey: 'op.reports.col.playerKind' },
      { key: 'state', labelKey: 'op.reports.col.state' },
      { key: 'duration', labelKey: 'op.reports.col.duration' },
      { key: 'revenue', labelKey: 'op.reports.col.revenue' }
    ],
    rows: rowsOf(result).map((r) => ({
      seat: readString(r, 'seatId'),
      device: readString(r, 'deviceId'),
      playerKind: readString(r, 'playerKind'),
      state: readString(r, 'state'),
      duration: minutes(r, 'durationSeconds', fmt),
      revenue: money(r, 'gameplayRevenue', fmt)
    }))
  };
}

export function buildOperatorActionReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.actions', value: fmt.formatNumber(readNumber(result, 'totalActionCount')) }
    ],
    columns: [
      { key: 'operator', labelKey: 'op.reports.col.operator' },
      { key: 'action', labelKey: 'op.reports.col.action' },
      { key: 'outcome', labelKey: 'op.reports.col.outcome' },
      { key: 'count', labelKey: 'op.reports.col.count' },
      { key: 'first', labelKey: 'op.reports.col.first' },
      { key: 'last', labelKey: 'op.reports.col.last' }
    ],
    rows: rowsOf(result).map((r) => ({
      operator: readString(r, 'actorDisplayName'),
      action: readString(r, 'action'),
      outcome: readString(r, 'outcome'),
      count: fmt.formatNumber(readNumber(r, 'count')),
      first: dateOrDash(r, 'firstAtUtc', fmt),
      last: dateOrDash(r, 'lastAtUtc', fmt)
    }))
  };
}
```

> Тест использует `MessageKey`-строки, которых ещё нет в каталоге i18n — на уровне `bun test` это неважно (строки), но `bun run build` (Task 5/6) потребует, чтобы ключи существовали. Ключи `op.reports.col.*`/`op.reports.sum.*` добавляются в Task 3 (Step i18n). Если реализуешь Task 2 изолированно и хочешь зелёный `tsc` уже сейчас — добавь ключи в Task 3 до сборки. `bun test` в этой задаче типы `MessageKey` не проверяет.

- [ ] **Step 4: Запустить — пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/history/reportModel.test.ts`
Expected: PASS (5 тестов). Если `readMoney`/`readString` имеют иную сигнатуру/поведение — поправить обёртки `money`/`dateOrDash` и повторить.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/reports/history/reportModel.ts src/AFK4.Operator.App.Web/src/reports/history/reportModel.test.ts
git commit -m "feat(operator): модель 4 отчётов истории (смены/касса/игровое время/действия)"
```

---

## Task 3: Экран «История» (реестр типов + хук + таблица + destination) + i18n

**Files:**
- Create: `src/reports/history/reportTypes.ts`, `src/reports/history/useReport.ts`, `src/reports/history/ReportTable.tsx`, `src/reports/history/HistoryDestination.tsx`
- Test: `src/reports/history/HistoryDestination.test.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`; регенерация `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: `buildShiftReportView`/`buildCashOperationReportView`/`buildGameplayTimeReportView`/`buildOperatorActionReportView` + `ReportView`/`ReportFormatters` (Task 2); `createAuthenticatedOperatorClients`/`downloadTextFile`/`formatMinorUnits` (operatorHelpers); `dateRange.ts` из `../../network/journal/dateRange` (`presetRange`/`isoToDateInput`/`dateInputToFromUtc`/`dateInputToToUtc`/`DateRange`); `OperatorBackendContext`; `ManagementScreen`; `EmptyState`/`Skeleton`.
- Produces:
  - `reportTypes.ts`: `type HistoryReportId = 'shifts' | 'cashOperations' | 'gameplayTime' | 'operatorActions'`; `interface HistoryReportSpec { id; labelKey; load(clients, branchId, range): Promise<Record<string, unknown>>; build(result, fmt): ReportView; exportCsv(clients, branchId, range): Promise<string>; csvName: string }`; `const historyReports: readonly HistoryReportSpec[]`.
  - `useReport.ts`: `type ReportState = {status:'loading'} | {status:'error'; retry} | {status:'ready'; view: ReportView; retry}`; `function useReport(load, build, fmt, deps): ReportState`.
  - `HistoryDestination.tsx`: `function HistoryDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element`.

- [ ] **Step 1: Добавить i18n-ключи (все три локали)**

Добавить в `locales/ru.json`, `locales/en.json`, `locales/tg.json` блок ключей (значения ниже — для ru; en/tg — реальные переводы, tg настоящий таджикский, НЕ копия ru):

Ключи (единый список для всех задач секции):
```
op.reports.dest.overview            = "Обзор"
op.reports.dest.overview.subtitle   = "Сводка за период сейчас"
op.reports.dest.history             = "История"
op.reports.dest.history.subtitle    = "Отчёты за диапазон дат"
op.reports.dest.journal             = "Журнал"
op.reports.dest.journal.subtitle    = "Аудит действий в филиале"
op.reports.noAccess                 = "Нет доступа к отчётам"
op.reports.history.tab.shifts       = "Смены"
op.reports.history.tab.cash         = "Кассовые операции"
op.reports.history.tab.gameplay     = "Игровое время"
op.reports.history.tab.actions      = "Действия операторов"
op.reports.export                   = "Экспорт CSV"
op.reports.empty                    = "Нет данных за выбранный период"
op.reports.limitNote                = "Показаны записи за выбранный период (лимит строк)."
op.reports.col.state                = "Статус"
op.reports.col.opened               = "Открыта"
op.reports.col.closed               = "Закрыта"
op.reports.col.movements            = "Движения"
op.reports.col.expectedCash         = "Ожидалось"
op.reports.col.countedCash          = "Насчитано"
op.reports.col.difference           = "Расхождение"
op.reports.col.source               = "Источник"
op.reports.col.opType               = "Тип операции"
op.reports.col.impact               = "Сумма"
op.reports.col.reason               = "Причина"
op.reports.col.created              = "Создано"
op.reports.col.seat                 = "Место"
op.reports.col.device               = "Устройство"
op.reports.col.playerKind           = "Игрок"
op.reports.col.duration             = "Длит., мин"
op.reports.col.revenue              = "Выручка"
op.reports.col.operator             = "Оператор"
op.reports.col.action               = "Действие"
op.reports.col.outcome              = "Исход"
op.reports.col.count                = "Кол-во"
op.reports.col.first                = "Первое"
op.reports.col.last                 = "Последнее"
op.reports.sum.cashIn               = "Приход"
op.reports.sum.cashOut              = "Расход"
op.reports.sum.netCash              = "Итого нал"
op.reports.sum.duration             = "Всего, мин"
op.reports.sum.package              = "Из пакета, мин"
op.reports.sum.bonus                = "Бонус, мин"
op.reports.sum.revenue              = "Выручка"
op.reports.sum.actions              = "Всего действий"
op.reports.journal.title            = "Журнал филиала"
op.reports.journal.subtitle         = "Аудит действий персонала в филиале"
op.reports.journal.empty            = "Записи не найдены"
op.reports.journal.actor.system     = "система"
```

Формат JSON-файлов локалей — плоский (`"op.reports.col.state": "Статус"`) или вложенный — определить по существующему `op.network.journal.*` в `locales/ru.json` и следовать ему. Регенерация:

Run: `cd src/AFK4.Operator.App.Web && bun run gen`
Expected: `packages/i18n/src/messages.ts` пересобран без ошибок.

Run: `cd src/AFK4.Operator.App.Web && bun test src/messages.test.ts`
Expected: PASS. Если гвард ругается на `tg === ru` для заимствования (напр. «Экспорт CSV», «Оператор») — добавить ключ в `TG_IDENTICAL_TO_RU_ALLOWED` в `src/messages.test.ts` ТОЛЬКО если это реальное заимствование; иначе исправить tg-перевод.

- [ ] **Step 2: Написать падающий тест `HistoryDestination.test.tsx`**

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const getShiftReport = mock(async () => ({
  rows: [{ state: 'Closed', openedAtUtc: '2026-07-01T08:00:00Z', closedAtUtc: '2026-07-01T20:00:00Z',
    cashMovementsTotal: { minorUnits: 5000, currencyCode: 'TJS' }, expectedCash: { minorUnits: 12000, currencyCode: 'TJS' },
    countedCash: { minorUnits: 12000, currencyCode: 'TJS' }, difference: { minorUnits: 0, currencyCode: 'TJS' } }]
}));
const getCashOperationReport = mock(async () => ({ cashInTotal: { minorUnits: 0, currencyCode: 'TJS' }, cashOutTotal: { minorUnits: 0, currencyCode: 'TJS' }, netCashTotal: { minorUnits: 0, currencyCode: 'TJS' }, rows: [] }));
const exportShiftReportCsv = mock(async () => 'state,opened\nClosed,2026-07-01');

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    shifts: { getShiftReport, getCashOperationReport, getGameplayTimeReport: async () => ({ rows: [] }), getOperatorActionReport: async () => ({ rows: [] }),
      exportShiftReportCsv, exportCashOperationReportCsv: async () => '', exportGameplayTimeReportCsv: async () => '', exportOperatorActionReportCsv: async () => '' }
  }),
  downloadTextFile: mock(() => {}),
  formatMinorUnits: (minor: number, code: string) => `${(minor / 100).toFixed(2)} ${code}`,
  readArray: (rec: Record<string, unknown>, key: string) => (Array.isArray(rec?.[key]) ? (rec[key] as unknown[]) : []),
  readRecord: (v: unknown) => (v && typeof v === 'object' ? (v as Record<string, unknown>) : {}),
  readString: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'string' ? (rec[key] as string) : ''),
  readNumber: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'number' ? (rec[key] as number) : 0),
  readMoney: (rec: Record<string, unknown>, key: string) => (rec?.[key] && typeof rec[key] === 'object' ? (rec[key] as { minorUnits: number; currencyCode: string }) : null)
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('HistoryDestination', () => {
  it('loads the shifts report by default and renders a row', async () => {
    const { HistoryDestination } = await import('./HistoryDestination');
    render(<I18nProvider initialLocale="ru"><HistoryDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(getShiftReport).toHaveBeenCalled());
    expect(await screen.findByText('50.00 TJS')).toBeInTheDocument();
  });

  it('switches to the cash tab and loads that report', async () => {
    const { HistoryDestination } = await import('./HistoryDestination');
    render(<I18nProvider initialLocale="ru"><HistoryDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(getShiftReport).toHaveBeenCalled());
    fireEvent.click(screen.getByText('Кассовые операции'));
    await waitFor(() => expect(getCashOperationReport).toHaveBeenCalled());
  });
});
```

> Мок `../../operatorHelpers` перечисляет `read*`-хелперы — иначе `reportModel` (импортирует их из того же модуля) получит `undefined`. `mock.module` в bun протекает по процессу — держать мок полным.

- [ ] **Step 3: Реализовать `useReport.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReportFormatters, ReportView } from './reportModel';

export type ReportState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; view: ReportView; retry: () => void };

export function useReport(
  load: () => Promise<Record<string, unknown>>,
  build: (result: Record<string, unknown>, fmt: ReportFormatters) => ReportView,
  fmt: ReportFormatters,
  deps: readonly unknown[]
): ReportState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [view, setView] = useState<ReportView | null>(null);
  const loadRef = useRef(load);
  loadRef.current = load;
  const buildRef = useRef(build);
  buildRef.current = build;
  const fmtRef = useRef(fmt);
  fmtRef.current = fmt;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    loadRef.current()
      .then((result) => {
        if (!cancelled) {
          setView(buildRef.current(result, fmtRef.current));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || view === null) return { status: 'loading' };
  return { status: 'ready', view, retry };
}
```

- [ ] **Step 4: Реализовать `reportTypes.ts`**

Сверить имена методов `createAuthenticatedOperatorClients(...).shifts.*` в `src/api/clients/shifts.ts` (подтверждены: `getShiftReport`, `getCashOperationReport`, `getGameplayTimeReport`, `getOperatorActionReport`, `exportShiftReportCsv`, `exportCashOperationReportCsv`, `exportGameplayTimeReportCsv`, `exportOperatorActionReportCsv`). Тип клиентов — вывести из возвращаемого `createAuthenticatedOperatorClients` (используем `ReturnType`).

```ts
import type { MessageKey } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { DateRange } from '../../network/journal/dateRange';
import type { ReportFormatters, ReportView } from './reportModel';
import {
  buildCashOperationReportView,
  buildGameplayTimeReportView,
  buildOperatorActionReportView,
  buildShiftReportView
} from './reportModel';

export type OperatorClients = ReturnType<typeof createAuthenticatedOperatorClients>;
export type HistoryReportId = 'shifts' | 'cashOperations' | 'gameplayTime' | 'operatorActions';

export interface HistoryReportSpec {
  id: HistoryReportId;
  labelKey: MessageKey;
  load: (clients: OperatorClients, branchId: string, range: DateRange) => Promise<Record<string, unknown>>;
  build: (result: Record<string, unknown>, fmt: ReportFormatters) => ReportView;
  exportCsv: (clients: OperatorClients, branchId: string, range: DateRange) => Promise<string>;
  csvName: string;
}

const q = (range: DateRange) => ({ fromUtc: range.fromUtc, toUtc: range.toUtc });

export const historyReports: readonly HistoryReportSpec[] = [
  {
    id: 'shifts',
    labelKey: 'op.reports.history.tab.shifts',
    load: (c, b, r) => c.shifts.getShiftReport(b, q(r)),
    build: buildShiftReportView,
    exportCsv: (c, b, r) => c.shifts.exportShiftReportCsv(b, q(r)),
    csvName: 'shifts'
  },
  {
    id: 'cashOperations',
    labelKey: 'op.reports.history.tab.cash',
    load: (c, b, r) => c.shifts.getCashOperationReport(b, q(r)),
    build: buildCashOperationReportView,
    exportCsv: (c, b, r) => c.shifts.exportCashOperationReportCsv(b, q(r)),
    csvName: 'cash-operations'
  },
  {
    id: 'gameplayTime',
    labelKey: 'op.reports.history.tab.gameplay',
    load: (c, b, r) => c.shifts.getGameplayTimeReport(b, q(r)),
    build: buildGameplayTimeReportView,
    exportCsv: (c, b, r) => c.shifts.exportGameplayTimeReportCsv(b, q(r)),
    csvName: 'gameplay-time'
  },
  {
    id: 'operatorActions',
    labelKey: 'op.reports.history.tab.actions',
    load: (c, b, r) => c.shifts.getOperatorActionReport(b, q(r)),
    build: buildOperatorActionReportView,
    exportCsv: (c, b, r) => c.shifts.exportOperatorActionReportCsv(b, q(r)),
    csvName: 'operator-actions'
  }
];
```

- [ ] **Step 5: Реализовать `ReportTable.tsx`**

```tsx
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../../operatorPrimitives';
import type { ReportView } from './reportModel';

export function ReportTable({ view, onExport }: { view: ReportView; onExport: () => void }): JSX.Element {
  const { t } = useI18n();
  const grid = view.columns.map(() => '1fr').join(' ');

  return (
    <div className="reports-history-body">
      <div className="reports-history-toolbar">
        <button type="button" className="ui-btn" onClick={onExport}>{t('op.reports.export')}</button>
      </div>

      {view.summaryCards.length > 0 && (
        <div className="reports-summary-grid">
          {view.summaryCards.map((card) => (
            <div key={card.labelKey} className="reports-summary-card">
              <span className="reports-summary-label">{t(card.labelKey)}</span>
              <strong className="reports-summary-value">{card.value}</strong>
            </div>
          ))}
        </div>
      )}

      {view.rows.length === 0 ? (
        <EmptyState title={t('op.reports.empty')} />
      ) : (
        <div className="table-panel">
          <div className="ctable-head" style={{ gridTemplateColumns: grid }} aria-hidden="true">
            {view.columns.map((col) => <span key={col.key}>{t(col.labelKey)}</span>)}
          </div>
          <div className="ctable-body">
            {view.rows.map((row, index) => (
              <div key={index} className="ctable-row" style={{ gridTemplateColumns: grid }}>
                {view.columns.map((col) => <span key={col.key}>{row[col.key]}</span>)}
              </div>
            ))}
          </div>
        </div>
      )}

      <p className="reports-history-limit-note">{t('op.reports.limitNote')}</p>
    </div>
  );
}
```

- [ ] **Step 6: Реализовать `HistoryDestination.tsx`**

```tsx
import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { createAuthenticatedOperatorClients, downloadTextFile, formatMinorUnits } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import { presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc, type DateRange } from '../../network/journal/dateRange';
import { historyReports, type HistoryReportId, type OperatorClients } from './reportTypes';
import { useReport } from './useReport';
import type { ReportFormatters } from './reportModel';
import { ReportTable } from './ReportTable';

export function HistoryDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatNumber, formatDate } = useI18n();
  const [activeId, setActiveId] = useState<HistoryReportId>('shifts');
  const [range, setRange] = useState<DateRange>(() => presetRange('30d', new Date()));

  const spec = historyReports.find((r) => r.id === activeId) ?? historyReports[0];

  const clients = useMemo<OperatorClients | null>(() => {
    if (backend === null) return null;
    return createAuthenticatedOperatorClients(backend.config, backend.session);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const fmt: ReportFormatters = { formatMinorUnits, formatNumber, formatDate };
  const branchId = backend?.branchId ?? '';

  const state = useReport(
    () => (clients === null ? Promise.resolve({}) : spec.load(clients, branchId, range)),
    spec.build,
    fmt,
    [activeId, branchId, range.fromUtc, range.toUtc, clients]
  );

  async function handleExport() {
    if (clients === null) return;
    const csv = await spec.exportCsv(clients, branchId, range);
    // downloadTextFile(fileName, contents) — имя первым (сверено в operatorHelpers.ts).
    downloadTextFile(`${spec.csvName}-${isoToDateInput(range.fromUtc)}_${isoToDateInput(range.toUtc)}.csv`, csv);
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.reports.dest.history')}
      subtitle={t('op.reports.dest.history.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      <div className="reports-history">
        <div className="reports-history-tabs">
          {historyReports.map((r) => (
            <button
              key={r.id}
              type="button"
              className={r.id === activeId ? 'ui-btn ui-btn--primary' : 'ui-btn'}
              onClick={() => setActiveId(r.id)}
            >
              {t(r.labelKey)}
            </button>
          ))}
        </div>

        <div className="reports-history-range mgmt-form">
          <label>
            {t('op.network.journal.range.from')}
            <input type="date" value={isoToDateInput(range.fromUtc)}
              onChange={(e) => setRange((prev) => ({ fromUtc: dateInputToFromUtc(e.currentTarget.value), toUtc: prev.toUtc }))} />
          </label>
          <label>
            {t('op.network.journal.range.to')}
            <input type="date" value={isoToDateInput(range.toUtc)}
              onChange={(e) => setRange((prev) => ({ fromUtc: prev.fromUtc, toUtc: dateInputToToUtc(e.currentTarget.value) }))} />
          </label>
        </div>

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true">
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
          </div>
        ) : state.status === 'ready' ? (
          <ReportTable view={state.view} onExport={handleExport} />
        ) : null}
      </div>
    </ManagementScreen>
  );
}
```

> Переиспользуем i18n-ключи диапазона из №2 (`op.network.journal.range.from/to`) — они уже существуют, не плодим новые. `ManagementScreen` сам рисует error-состояние по `state='error'` + `onRetry`.

- [ ] **Step 7: Добавить CSS-класс раздела**

Создать `src/styles/26-reports.css` (проверить свободный номер: `ls src/styles/`) с минимальной раскладкой (`.reports-history`, `.reports-history-tabs`, `.reports-history-range`, `.reports-summary-grid`, `.reports-summary-card`, `.reports-history-toolbar`, `.reports-history-limit-note`) в стиле `25-network.css`. Подключить импорт там же, где подключаются остальные `styles/*.css` (найти `grep -rn "25-network.css" src/`).

Минимальный контент (адаптировать под существующие переменные токенов):
```css
.reports-history { display: flex; flex-direction: column; gap: 16px; }
.reports-history-tabs { display: flex; flex-wrap: wrap; gap: 8px; }
.reports-history-range { display: flex; gap: 12px; flex-wrap: wrap; }
.reports-history-toolbar { display: flex; justify-content: flex-end; }
.reports-summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; }
.reports-summary-card { display: flex; flex-direction: column; gap: 4px; padding: 12px 14px; border-radius: var(--radius-card, 12px); background: var(--surface-raised, #fff); box-shadow: var(--shadow-card); }
.reports-summary-label { font-size: 12px; color: var(--text-muted); }
.reports-summary-value { font-size: 18px; font-variant-numeric: tabular-nums; }
.reports-history-limit-note { font-size: 12px; color: var(--text-muted); }
```

- [ ] **Step 8: Запустить тест — пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/history/HistoryDestination.test.tsx`
Expected: PASS (2 теста).

- [ ] **Step 9: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/reports/history src/AFK4.Operator.App.Web/src/styles src/AFK4.Operator.App.Web/locales packages/i18n/src/messages.ts src/AFK4.Operator.App.Web/src/messages.test.ts
git commit -m "feat(operator): экран «История» — 4 отчёта с диапазоном дат и CSV"
```

---

## Task 4: Экран «Журнал» филиала (`reports/journal/`)

**Files:**
- Create: `src/reports/journal/useBranchAudit.ts`, `src/reports/journal/BranchJournalDestination.tsx`
- Test: `src/reports/journal/BranchJournalDestination.test.tsx`

**Interfaces:**
- Consumes: `createAuthenticatedOperatorClients` (operatorHelpers) → `.audit.search(request)`; `readArray`/`readString`/`readRecord` (для маппинга нетипизированного ответа в `OrgAuditRecordDto`); `OrgAuditRecordDto` тип из `../../api/clients/orgAudit`; `toAuditRows`/`outcomeChipTone` из `../../network/journal/orgAuditModel`; `OrgAuditFilters`/`AuditDraft` из `../../network/journal/OrgAuditFilters`; `presetRange`/`DateRange` из `../../network/journal/dateRange`; `ManagementScreen`; `EmptyState`.
- Produces:
  - `useBranchAudit.ts`: `interface BranchAuditClient { search(branchId: string, query: BranchAuditQuery): Promise<OrgAuditRecordDto[]> }`; `type BranchAuditQuery = { fromUtc; toUtc; action?; outcome?; targetType?; limit? }`; `type BranchAuditState`; `function useBranchAudit(client, branchId, query): BranchAuditState`.
  - `BranchJournalDestination.tsx`: `function BranchJournalDestination({ backend }): JSX.Element`.

**Замечание:** ответ `audit.search` — `Record<string, unknown>` с массивом `records` тех же полей, что `OrgAuditRecordDto` (`auditRecordId`/`action`/`outcome`/`targetType`/`targetId`/`actorStaffUserId`/`actorPlatformAdminUserId`/`sourceApp`/`detailsJson`/`createdAtUtc`/`branchId`). Маппим через `read*`-хелперы (не кастуем вслепую), затем переиспользуем `toAuditRows` из №2.

- [ ] **Step 1: Написать падающий тест `BranchJournalDestination.test.tsx`**

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const search = mock(async () => ({
  records: [{
    auditRecordId: 'r1', branchId: 'b1', actorStaffUserId: 'staff-1', actorPlatformAdminUserId: null,
    action: 'shift.opened', targetType: 'Shift', targetId: 's1', outcome: 'Succeeded',
    sourceApp: 'OperatorApp', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
  }]
}));

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({ audit: { search } }),
  readArray: (rec: Record<string, unknown>, key: string) => (Array.isArray(rec?.[key]) ? (rec[key] as unknown[]) : []),
  readRecord: (v: unknown) => (v && typeof v === 'object' ? (v as Record<string, unknown>) : {}),
  readString: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'string' ? (rec[key] as string) : '')
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('BranchJournalDestination', () => {
  it('searches BRANCH audit (not org) and renders a row', async () => {
    const { BranchJournalDestination } = await import('./BranchJournalDestination');
    render(<I18nProvider initialLocale="ru"><BranchJournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('shift.opened')).toBeInTheDocument());
    // Верифицируем именно branch-запрос: search вызван с branchId 'b1' первым аргументом.
    expect(search).toHaveBeenCalled();
    const firstArg = (search.mock.calls[0] as unknown[])[0];
    expect(firstArg).toMatchObject({ branchId: 'b1' });
    expect(screen.getByText('Shift (s1)')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Запустить — упасть**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/journal/BranchJournalDestination.test.tsx`
Expected: FAIL — модуль не найден.

- [ ] **Step 3: Реализовать `useBranchAudit.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

export interface BranchAuditQuery {
  fromUtc: string;
  toUtc: string;
  action?: string;
  outcome?: string;
  targetType?: string;
  limit?: number;
}

export interface BranchAuditClient {
  search(branchId: string, query: BranchAuditQuery): Promise<OrgAuditRecordDto[]>;
}

export type BranchAuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: OrgAuditRecordDto[]; retry: () => void };

export function useBranchAudit(client: BranchAuditClient, branchId: string, query: BranchAuditQuery): BranchAuditState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [records, setRecords] = useState<OrgAuditRecordDto[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);
  const queryKey = JSON.stringify(query);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.search(branchId, query)
      .then((res) => { if (!cancelled) { setRecords(res); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
```

- [ ] **Step 4: Реализовать `BranchJournalDestination.tsx`**

```tsx
import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients, readArray, readString } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';
import { presetRange, type DateRange } from '../../network/journal/dateRange';
import { OrgAuditFilters, type AuditDraft } from '../../network/journal/OrgAuditFilters';
import { toAuditRows } from '../../network/journal/orgAuditModel';
import { useBranchAudit, type BranchAuditClient, type BranchAuditQuery } from './useBranchAudit';

const DEFAULT_LIMIT = 100;
const GRID = '1.2fr 1fr 1.4fr 1.2fr 0.8fr 0.8fr 1.4fr';

function mapRecords(result: Record<string, unknown>): OrgAuditRecordDto[] {
  // Каждый элемент `records` — уже объект-запись; читаем поля прямо (readArray<Record> + readString).
  return readArray<Record<string, unknown>>(result, 'records').map((r) => {
    const str = (key: string) => readString(r, key);
    const nullable = (key: string) => (str(key) === '' ? null : str(key));
    return {
      auditRecordId: str('auditRecordId'),
      branchId: nullable('branchId'),
      actorStaffUserId: nullable('actorStaffUserId'),
      actorPlatformAdminUserId: nullable('actorPlatformAdminUserId'),
      action: str('action'),
      targetType: str('targetType'),
      targetId: nullable('targetId'),
      outcome: str('outcome'),
      sourceApp: str('sourceApp'),
      detailsJson: str('detailsJson'),
      createdAtUtc: str('createdAtUtc')
    };
  });
}

function buildQuery(range: DateRange, draft: AuditDraft): BranchAuditQuery {
  const q: BranchAuditQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) q.action = draft.action;
  if (draft.targetType.length > 0) q.targetType = draft.targetType;
  if (draft.outcome !== 'all') q.outcome = draft.outcome;
  return q;
}

// Аудит филиала (Отчёты → Журнал): менеджерский branch-scoped журнал. Endpoint
// /api/branches/{id}/audit идёт через RequireBranchPermissionAsync (фундамент №1) — per-branch,
// утечки чужих филиалов нет (в отличие от org-журнала в Сеть→Журнал). Переиспользует фильтры и
// модель строк из network/journal (та же форма записи).
export function BranchJournalDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<BranchAuditQuery>(() =>
    buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' })
  );

  const client = useMemo<BranchAuditClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return {
      search: (branchId, q) =>
        clients.audit.search({ branchId, action: q.action, outcome: q.outcome, targetType: q.targetType, fromUtc: q.fromUtc, toUtc: q.toUtc, limit: q.limit }).then(mapRecords)
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBranchAudit(
    client ?? { search: async () => [] },
    backend?.branchId ?? '',
    query
  );

  const rows = state.status === 'ready' ? toAuditRows(state.records, { formatDate }, t('op.reports.journal.actor.system')) : [];

  function handleRange(next: DateRange) {
    setRange(next);
    setQuery((prev) => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.reports.journal.title')}
      subtitle={t('op.reports.journal.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      <div className="network-journal">
        <OrgAuditFilters
          range={range}
          onRangeChange={handleRange}
          onApply={(draft) => setQuery(buildQuery(range, draft))}
          onReset={() => setQuery(buildQuery(range, { action: '', outcome: 'all', targetType: '' }))}
        />

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true">
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState title={t('op.reports.journal.empty')} />
        ) : (
          <div className="table-panel">
            <div className="ctable-head" style={{ gridTemplateColumns: GRID }} aria-hidden="true">
              <span>{t('op.network.journal.col.date')}</span>
              <span>{t('op.network.journal.col.actor')}</span>
              <span>{t('op.network.journal.col.action')}</span>
              <span>{t('op.network.journal.col.target')}</span>
              <span>{t('op.network.journal.col.outcome')}</span>
              <span>{t('op.network.journal.col.source')}</span>
              <span>{t('op.network.journal.col.details')}</span>
            </div>
            <div className="ctable-body">
              {rows.map((row) => (
                <div key={row.id} className="ctable-row" style={{ gridTemplateColumns: GRID }}>
                  <span>{row.date}</span>
                  <span>{row.actor}</span>
                  <span className="network-journal-action">{row.action}</span>
                  <span>{row.target}</span>
                  <span className={`ui-chip ui-chip--status ${row.outcomeTone}`}>{row.outcome}</span>
                  <span>{row.source}</span>
                  <span className="network-journal-details" title={row.details}>{row.details}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </ManagementScreen>
  );
}
```

> Переиспользуем CSS-классы `network-journal*` из `25-network.css` (не плодим новые). i18n колонок/фильтров — существующие `op.network.journal.*`. Новые ключи `op.reports.journal.*` (title/subtitle/empty/actor.system) уже добавлены в Task 3 Step 1.

- [ ] **Step 5: Запустить тест — пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/journal/BranchJournalDestination.test.tsx`
Expected: PASS (1 тест).

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/reports/journal
git commit -m "feat(operator): экран «Журнал» филиала (branch-audit, переиспользование UI Сети)"
```

---

## Task 5: Switcher `ReportsWorkspace` + перенос «Обзора» + проводка

**Files:**
- Create: `src/reports/ReportsWorkspace.tsx`, `src/reports/overview/OverviewDestination.tsx`
- Modify: `src/DashboardWorkspace.tsx` (корень `<main>`→`<section>`), `src/WorkspaceRouter.tsx` (`dashboard`→`ReportsWorkspace`), `src/operatorPermissions.ts` (`dashboard` rule += `viewAudit`)
- Test: `src/reports/ReportsWorkspace.test.tsx`; Modify `test/operatorVisibility.test.ts`

**Interfaces:**
- Consumes: `allowedReportsDestinations`/`ReportsDestinationId` (Task 1); `OverviewDestination`/`HistoryDestination`/`BranchJournalDestination`; `OperatorBackendContext`/`WorkspaceId`; `EmptyState`.
- Produces:
  - `OverviewDestination.tsx`: `function OverviewDestination(props): JSX.Element` — рендерит существующий `DashboardWorkspace` с его пропсами.
  - `ReportsWorkspace.tsx`: `function ReportsWorkspace({ backend, currencyCode, onNavigate, onOpenSeat }): JSX.Element`.

- [ ] **Step 1: Перенести корень `DashboardWorkspace` `<main>`→`<section>`**

В `src/DashboardWorkspace.tsx` заменить корневой `<main className="workspace-screen dashboard-screen">` (строка ~310) на `<section className="workspace-screen dashboard-screen">` и закрывающий `</main>` (строка ~442) на `</section>`. Больше НИЧЕГО в логике не менять (перенос без изменения поведения — switcher-пейн уже даёт контейнер, `<main>` во вложенном destination невалиден, как и у `ManagementScreen`, который тоже `<section>`).

- [ ] **Step 2: Создать `OverviewDestination.tsx`**

```tsx
import type { JSX } from 'react';
import { DashboardWorkspace } from '../../DashboardWorkspace';
import type { OperatorBackendContext, WorkspaceId } from '../../operatorTypes';

export function OverviewDestination({ backend, currencyCode, onNavigate, onOpenSeat }: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}): JSX.Element {
  return <DashboardWorkspace currencyCode={currencyCode} backend={backend} onNavigate={onNavigate} onOpenSeat={onOpenSeat} />;
}
```

- [ ] **Step 3: Написать падающий тест `ReportsWorkspace.test.tsx`**

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

// Заглушки трёх destination — проверяем сам switcher (навигация/видимость), не их внутренности.
mock.module('./overview/OverviewDestination', () => ({ OverviewDestination: () => <div>OVERVIEW</div> }));
mock.module('./history/HistoryDestination', () => ({ HistoryDestination: () => <div>HISTORY</div> }));
mock.module('./journal/BranchJournalDestination', () => ({ BranchJournalDestination: () => <div>JOURNAL</div> }));

function backendWith(permissions: string[]) {
  return { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { permissions, accessToken: 't', organizationId: 'org' }, branchId: 'b1' } as never;
}

describe('ReportsWorkspace', () => {
  it('renders first allowed destination (overview) by default', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['reports.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.getByText('OVERVIEW')).toBeInTheDocument();
  });

  it('shows only journal when session has audit.view alone', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['audit.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.getByText('JOURNAL')).toBeInTheDocument();
    expect(screen.queryByText('OVERVIEW')).not.toBeInTheDocument();
  });

  it('renders no-access when session lacks both permissions', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith([])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.queryByText('OVERVIEW')).not.toBeInTheDocument();
    expect(screen.queryByText('HISTORY')).not.toBeInTheDocument();
    expect(screen.queryByText('JOURNAL')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 4: Реализовать `ReportsWorkspace.tsx`** (по образцу `NetworkWorkspace.tsx`)

```tsx
import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { allowedReportsDestinations, type ReportsDestinationId } from './reportsNav';
import { OverviewDestination } from './overview/OverviewDestination';
import { HistoryDestination } from './history/HistoryDestination';
import { BranchJournalDestination } from './journal/BranchJournalDestination';

export function ReportsWorkspace({ backend, currencyCode, onNavigate, onOpenSeat }: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}): JSX.Element {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const destinations = allowedReportsDestinations(session);
  const [active, setActive] = useState<ReportsDestinationId | null>(destinations[0]?.id ?? null);

  if (destinations.length === 0) {
    return (
      <section className="workspace-screen">
        <EmptyState title={t('op.reports.noAccess')} />
      </section>
    );
  }

  const currentId: ReportsDestinationId = destinations.some((d) => d.id === active)
    ? (active as ReportsDestinationId)
    : destinations[0].id;

  function renderActive(): JSX.Element {
    switch (currentId) {
      case 'overview':
        return <OverviewDestination backend={backend} currencyCode={currencyCode} onNavigate={onNavigate} onOpenSeat={onOpenSeat} />;
      case 'history':
        return <HistoryDestination backend={backend} />;
      case 'journal':
        return <BranchJournalDestination backend={backend} />;
    }
  }

  return (
    <div className="management-layout">
      <nav className="management-nav">
        {destinations.map((destination) => {
          const Icon = destination.Icon;
          return (
            <button
              key={destination.id}
              type="button"
              className={destination.id === currentId ? 'active' : undefined}
              onClick={() => setActive(destination.id)}
            >
              <Icon size={16} aria-hidden="true" />
              <span>{t(destination.labelKey)}</span>
            </button>
          );
        })}
      </nav>
      <div className="management-active-pane">{renderActive()}</div>
    </div>
  );
}
```

- [ ] **Step 5: Провести в `WorkspaceRouter.tsx`**

Заменить импорт `DashboardWorkspace` на `ReportsWorkspace` (`import { ReportsWorkspace } from './reports/ReportsWorkspace';`) и ветку `workspace === 'dashboard'`:

```tsx
      {workspace === 'dashboard' && (
        <ReportsWorkspace
          backend={backend}
          currencyCode={currencyCode}
          onNavigate={onNavigate}
          onOpenSeat={onOpenSeat}
        />
      )}
```

(`session` этой ветке больше не нужен; `DashboardWorkspace` теперь импортируется внутри `OverviewDestination`.)

- [ ] **Step 6: Расширить право секции в `operatorPermissions.ts`**

```ts
  dashboard: [permissionNames.viewReports, permissionNames.viewAudit],
```

(Секция «Отчёты» теперь доступна при `reports.view` ИЛИ `audit.view` — для менеджера-аудитора без reports.view открывается сразу Журнал.)

- [ ] **Step 7: Обновить `test/operatorVisibility.test.ts`**

Найти ожидания по `dashboard`/`logs` (`grep -n "dashboard\|logs" test/operatorVisibility.test.ts`). Обновить: `canOpenWorkspace(session, 'dashboard')` истинно при `reports.view` ИЛИ `audit.view`. (Удаление `'logs'` — в Task 6; здесь если тест ссылается на `dashboard`-правило — привести к новому union.)

- [ ] **Step 8: Запустить тесты — пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/reports/ReportsWorkspace.test.tsx test/operatorVisibility.test.ts`
Expected: PASS.

- [ ] **Step 9: Сборка (тайпчек всей секции)**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: `tsc -b` + `vite build` без ошибок. Типичные ловушки: несуществующие `MessageKey` (все `op.reports.*` должны быть в `messages.ts` после Task 3 gen), несовпадение сигнатур `createAuthenticatedOperatorClients(...).shifts.*`.

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/reports src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/test/operatorVisibility.test.ts
git commit -m "feat(operator): «Отчёты» → switcher (Обзор/История/Журнал), Обзор как destination"
```

---

## Task 6: Снос мёртвого `logs`

**Files:**
- Delete: `src/BackendLogsWorkspace.tsx` (+ тест, если есть — `grep -rl "BackendLogsWorkspace" src test`)
- Modify: `src/operatorTypes.ts` (убрать `'logs'` из `WorkspaceId`), `src/operatorPermissions.ts` (убрать `'logs'` из `workspaceIds` и из `workspacePermissionRules`), `src/WorkspaceRouter.tsx` (убрать импорт + ветку `workspace === 'logs'`)
- Test: обновить `test/operatorVisibility.test.ts` / любые тесты, ссылающиеся на `'logs'`

**Interfaces:**
- Produces: `WorkspaceId` без `'logs'` — тип сузился, все `switch`/записи `Record<WorkspaceId, …>` обязаны перестать упоминать `'logs'` (иначе `tsc` упадёт — это и есть страховка).

- [ ] **Step 1: Найти все ссылки**

Run: `cd src/AFK4.Operator.App.Web && grep -rn "'logs'\|\"logs\"\|BackendLogsWorkspace" src test`
Зафиксировать список — все должны быть удалены/обновлены к концу задачи.

- [ ] **Step 2: Удалить файл(ы) `BackendLogsWorkspace`**

```bash
git rm src/AFK4.Operator.App.Web/src/BackendLogsWorkspace.tsx
# если есть тест: git rm src/AFK4.Operator.App.Web/src/BackendLogsWorkspace.test.tsx
```

- [ ] **Step 3: Убрать `'logs'` из `WorkspaceId` (`operatorTypes.ts`)**

```ts
export type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'cash' | 'players' | 'management' | 'stock' | 'network';
```

- [ ] **Step 4: Убрать `'logs'` из `operatorPermissions.ts`**

Из `workspaceIds` убрать `'logs'`; из `workspacePermissionRules` удалить строку `logs: [...]`. (`Record<WorkspaceId, …>` теперь требует ровно оставшиеся ключи — `tsc` проверит.)

- [ ] **Step 5: Убрать импорт и ветку в `WorkspaceRouter.tsx`**

Удалить `import { BackendLogsWorkspace } from './BackendLogsWorkspace';` и строку `{workspace === 'logs' && <BackendLogsWorkspace ... />}`.

- [ ] **Step 6: Обновить тесты, ссылающиеся на `'logs'`**

Из `test/operatorVisibility.test.ts` и прочих — убрать `'logs'` из ожидаемых наборов workspace-ов. (По списку из Step 1.)

- [ ] **Step 7: Сборка + тесты**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: без ошибок (висячие ссылки на `'logs'`/`BackendLogsWorkspace` → `tsc` покажет — почистить).

Run: `cd src/AFK4.Operator.App.Web && bun test`
Expected: весь фронт-набор зелёный.

- [ ] **Step 8: Коммит**

```bash
git add -A
git commit -m "refactor(operator): снести мёртвый недостижимый воркспейс logs"
```

---

## Task 7: Аудит паритета дублей (делегированная верификация, без кода)

**Цель:** доказать «оператор ≥ Platform.Web/club» по каждому дублю, чтобы №4 удалял безопасно. Это верификация — код не пишется (кроме починки мелких дыр, если найдутся).

**Files:**
- Create: `docs/superpowers/notes/2026-07-24-operator-parity-audit.md` (отчёт-«сертификат» покрытия)

- [ ] **Step 1: Делегировать поэкранное сравнение**

Запустить агентов (sonnet, НЕ opus) — по одному на экран-дубль, каждый сравнивает возможности Platform.Web `/club/<screen>` с соответствующим экраном оператора и возвращает список расхождений (что есть в Platform.Web и НЕ покрыто оператором). Экраны:
- Clients: `AFK4.Platform.Web/src/club/clients/*` vs оператор `players/*` (+ `BackendPlayersWorkspace`).
- Monetization: `.../club/monetization/*` vs Управление→Тарифы+Пакеты (`management/destinations/TariffsPackagesDestination`) и Товары (`GoodsDestination`).
- Settings: `.../club/settings/*` vs Управление→Клуб (`ClubDestination`) и Сотрудники (`StaffRolesDestination`).
- Venue: `.../club/venue/*` vs Карта (`MapWorkspace`) + Управление→Залы и ПК (`HallsDevicesDestination`). ЯВНО исключить из «дыр»: floor-map drag&drop редактор, подтверждение устройств (pending approve/reject), тумблер ручного подтверждения — это осознанный descope, не дыра.

Каждому агенту дать: путь Platform.Web-экрана, путь оператор-экрана(ов), и требование вернуть таблицу «возможность → покрыта? (да/нет/частично) → где в операторе / чего не хватает».

- [ ] **Step 2: Свести находки и классифицировать**

Собрать отчёт в `docs/superpowers/notes/2026-07-24-operator-parity-audit.md`: таблица по экранам + вердикт. Классификация:
- **Мелкая дыра** (напр. отсутствующее поле формы, лейбл) → починить в рамках №3 отдельным коммитом.
- **Крупная дыра** (целый под-экран/действие) → НЕ чинить молча: зафиксировать в отчёте, вынести пользователю решение (в №3 или отложить в №4).
- **Осознанный descope** → отметить как «намеренно не переносим».

- [ ] **Step 3: Починить мелкие дыры (если есть)**

Для каждой мелкой дыры — обычный TDD-цикл (тест → фикс → зелёный), отдельный коммит `fix(operator): паритет с Platform.Web — <что>`.

- [ ] **Step 4: Коммит отчёта**

```bash
git add docs/superpowers/notes/2026-07-24-operator-parity-audit.md
git commit -m "docs(operator): сертификат паритета дублей оператор ≥ Platform.Web/club"
```

- [ ] **Step 5: Финальные гейты слайса**

Run: `cd src/AFK4.Operator.App.Web && bun test && bun run build`
Expected: всё зелёное. App.test прогнать отдельно, если он изолирован (`bun test src/App.test.tsx`).

---

## Self-Review (заполняется автором плана)

**1. Покрытие спеки:**
- §3 объём (4 отчёта, продажи исключены) → Task 2/3 (ровно shifts/cash/gameplay/actions). ✓
- §4.1 switcher на `dashboard` id → Task 1 (nav) + Task 5 (host+проводка). ✓
- §4.2 перенос Обзора без изменений → Task 5 Step 1-2 (`<main>`→`<section>`, обёртка). ✓
- §4.3 История (reportTypes/useReport/reportModel/ReportTable/dateRange/CSV) → Task 2+3. ✓
- §4.4 Журнал branch-scoped, переиспользование №2 → Task 4. ✓
- §4.5 снос logs → Task 6. ✓
- §5 аудит паритета → Task 7. ✓
- §6 i18n три локали + guard → Task 3 Step 1. ✓
- §7 тесты (reportsNav/reportModel/History/BranchJournal/visibility) → покрыты. ✓

**2. Плейсхолдеры:** нет TBD/«обработать ошибки»/«аналогично Task N» — код приведён целиком. ✓ Единственные явные «сверить сигнатуру» — по `read*`-хелперам и именам методов клиента: это точечные проверки фактов в существующем коде перед использованием, с указанием точной команды `grep`, не заглушки логики.

**3. Согласованность типов:**
- `ReportView`/`ReportFormatters` определены в Task 2, потребляются Task 3 (useReport/reportTypes/ReportTable) — сигнатуры совпадают (`formatMinorUnits`/`formatNumber`/`formatDate`). ✓
- `ReportsDestinationId` (Task 1) ↔ switch в `ReportsWorkspace` (Task 5) — три ветки overview/history/journal. ✓
- `OrgAuditRecordDto` (из orgAudit №2) переиспользуется как форма записи branch-аудита (Task 4) — поля идентичны. ✓
- `HistoryReportSpec.load/exportCsv` сигнатуры ↔ вызовы `clients.shifts.*` (Task 3) — имена методов сверяются в Step 4. ✓
- `dashboard` остаётся в `WorkspaceId` (Task 5); `logs` удаляется (Task 6) — порядок верный (visibility-тест трогается дважды, это ок). ✓
