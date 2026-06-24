# Касса S2 — «Журнал кассы» + X/Z-отчёты + чистка op.payments.* — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Достроить раздел «Касса»: вкладка «Журнал кассы» (лента кассовых операций + аппрув возвратов/коррекций), X-отчёт (read-only снимок смены) и Z-сводка (печатная форма при закрытии); вычистить 98 осиротевших ключей `op.payments.*`.

**Architecture:** Вкладка `review` переименовывается в `journal` («Журнал кассы») = `CashJournalWorkspace` с сегментами «Кассовые операции» (новый `CashOperationsLedger`, поверх `getCashOperationReport`) и «Проверка» (существующий `ReviewWorkspace` во встроенном режиме). X/Z — общая презентационная `ShiftReportModal` + чистые билдеры текста для печати в `cash/shiftReport.ts`; X — кнопка в командной панели шапки, Z — модалка после успешного закрытия. Бэк не трогаем (всё поверх готовых эндпоинтов).

**Tech Stack:** React + TypeScript, `@afk4/i18n` (ICU), `bun test` (happy-dom + jest-dom), lucide-react, существующие хелперы `operatorHelpers.ts`, примитив `PanelModal`.

## Global Constraints

- **Деньги:** `formatMoney(value, currencyCode)` рендерит TJS целыми с символом «с.» («115 с.», НЕ «115,00 TJS»); `formatMoney(null, cc)` → «0 с.» → в денежных строках гардить null→placeholder, иначе ложно «сошлось». Парсинг сумм: `parseMoneyInputMinorUnits` (строго >0), `parseNonNegativeMoneyInputMinorUnits` (≥0). DTO — minor units.
- **i18n:** каждый новый ключ — реально в ru/en/tg; tg — настоящий таджикский (смена = «навбат»), НЕ копия ru. После правки `locales/{ru,en,tg}.json` — `cd packages/i18n && bun run gen` (регенерация `messages.ts`, файл авто-генерируемый, руками не править) + `bun test` (guard tg≠ru).
- **Тест-паттерн боевого клиента:** компоненты, строящие `createAuthenticatedOperatorClients`, принимают инъект-клиент и строят боевой только при `backend && !injected` (фейк-backend `config:'x'` бросает «Invalid URL» в `PlatformApiClient` на init) — либо строят лениво в обработчике, не на рендере.
- **App.test флак:** действие через nonce-бамп (`onShiftChanged → shiftNonce++`) порождает «вторую волну» рефетчей шапки+кокпита; тест обязан её дренировать (await пост-действенного рефетча, напр. счётчик `/shifts/revenue/current` `>= before+2`), иначе async утекает в соседние тесты.
- **App.test гонять ОТДЕЛЬНЫМ `bun test`-вызовом** (утечка `mock.module` process-wide). Гейт `bun run test` = subdir-прогон `&&` App.test.
- **Money-path бэка не меняем.** Никаких новых сущностей/миграций (Z = презентация результата `close`; X = презентация `revenue/current`).
- **Никаких AI-подписей** в коде/коммитах/PR.

**Гейты (команды):**
- Фронт subdir + App.test: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
- Фронт build (tsc+vite): `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
- i18n: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`

**Файловая раскладка (что создаём / меняем):**
- Создаём: `cash/shiftReport.ts`, `cash/shiftReport.test.ts`, `cash/ShiftReportModal.tsx`, `cash/ShiftReportModal.test.tsx`, `cash/CashOperationsLedger.tsx`, `cash/CashOperationsLedger.test.tsx`, `cash/CashJournalWorkspace.tsx`, `cash/CashJournalWorkspace.test.tsx`.
- Меняем: `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.ts` (ген), `cash/CashShiftCommandBar.tsx` (+ `.test`), `cash/CashShiftHeader.tsx`, `ReviewWorkspace.tsx` (флаг `embedded`), `cash/CashTabBar.tsx`, `cash/cashModel.ts` (+ `.test`), `cash/CashWorkspace.tsx`, `styles/21-cash.css`, `src/App.test.tsx`.

**Источники (карта бэка/типов, проверено):**
- `shiftRevenue.current(branchId): Promise<ShiftRevenueDto | null>`, `history(branchId, limit=20)`. `ShiftRevenueDto = { shiftId, organizationId, branchId, openedByStaffUserId, closedByStaffUserId, state, earned:{time,goods,total}, inflow:{cash,nonCash,walletTopUps,directTotal}, cash:{starting,expected,counted,difference}, openedAtUtc, closedAtUtc }` (Money = `{currencyCode,minorUnits}`).
- `shifts.getCashOperationReport(branchId, query?): Promise<Record<string,unknown>>` → `{ rows[], cashInTotal, cashOutTotal, netCashTotal, limit }`; row = `{ operationId, createdAtUtc, operationType, cashImpact:Money, reason, sourceType, shiftId, createdByStaffUserId }`. CSV: `shifts.exportCashOperationReportCsv(branchId, query?)`.
- `shifts.closeShift(shiftId, req): Promise<ShiftDto>` (ShiftDto = `Record<string,unknown>`), поля результата: `countedCash:Money`, `difference:Money`, `closingNote`, `closedAtUtc` (+ `startingCash`, `expectedCash`). Выручки (earned/inflow) в ответе close НЕТ — берём из снимка revenue.
- Хелперы (`operatorHelpers.ts`): `formatMoney`, `formatTime`, `cashOperationTypeLabel`, `escapeHtml`, `downloadTextFile`, `readArray`, `readMoney`, `readString`, `createAuthenticatedOperatorClients`, `hasPermission`, `hasAnyPermission`, `permissionNames`.

---

### Task 1: i18n — ключи «Журнала кассы» + X/Z + чистка op.payments.*

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify (ген): `packages/i18n/src/messages.ts`

**Interfaces:**
- Produces: новые ключи `op.cash.action.xReport`, `op.cash.journal.*`, `op.cash.report.*` — используются в Tasks 3–7. Удаляются все `op.payments.*` (98 шт., нигде не используются — проверено `grep -rl "op.payments." src` → пусто).

- [ ] **Step 1: Добавить новые ключи в `locales/ru.json`** сразу после строки `"op.cash.shift.empty": "Нет открытой смены",` (≈ строка 986):

```json
  "op.cash.action.xReport": "X-отчёт",
  "op.cash.journal.tab": "Журнал кассы",
  "op.cash.journal.title": "Журнал кассы",
  "op.cash.journal.heading": "Операции и проверка",
  "op.cash.journal.segOps": "Кассовые операции",
  "op.cash.journal.segReview": "Проверка",
  "op.cash.journal.searchPlaceholder": "Поиск по причине или типу",
  "op.cash.journal.empty": "Кассовых операций нет",
  "op.cash.journal.noMatch": "Ничего не найдено",
  "op.cash.journal.loading": "Загрузка операций…",
  "op.cash.journal.cashIn": "Внесено",
  "op.cash.journal.cashOut": "Изъято",
  "op.cash.journal.net": "Итого по кассе",
  "op.cash.journal.export": "Экспорт (CSV)",
  "op.cash.report.xTitle": "X-отчёт",
  "op.cash.report.zTitle": "Z-отчёт",
  "op.cash.report.xSubtitle": "промежуточный отчёт по смене",
  "op.cash.report.zSubtitle": "итоговый отчёт по закрытию",
  "op.cash.report.print": "Печать",
  "op.cash.report.revenueSection": "Выручка смены",
  "op.cash.report.reconcileSection": "Сверка кассы",
  "op.cash.report.opened": "Открыта",
  "op.cash.report.closed": "Закрыта",
  "op.cash.report.printHeader": "AFK4 · Отчёт по смене",
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`** (после `"op.cash.shift.empty"`):

```json
  "op.cash.action.xReport": "X report",
  "op.cash.journal.tab": "Cash journal",
  "op.cash.journal.title": "Cash journal",
  "op.cash.journal.heading": "Operations & review",
  "op.cash.journal.segOps": "Cash operations",
  "op.cash.journal.segReview": "Review",
  "op.cash.journal.searchPlaceholder": "Search by reason or type",
  "op.cash.journal.empty": "No cash operations",
  "op.cash.journal.noMatch": "Nothing found",
  "op.cash.journal.loading": "Loading operations…",
  "op.cash.journal.cashIn": "Cash in",
  "op.cash.journal.cashOut": "Cash out",
  "op.cash.journal.net": "Cash total",
  "op.cash.journal.export": "Export (CSV)",
  "op.cash.report.xTitle": "X report",
  "op.cash.report.zTitle": "Z report",
  "op.cash.report.xSubtitle": "interim shift report",
  "op.cash.report.zSubtitle": "shift close summary",
  "op.cash.report.print": "Print",
  "op.cash.report.revenueSection": "Shift revenue",
  "op.cash.report.reconcileSection": "Cash reconciliation",
  "op.cash.report.opened": "Opened",
  "op.cash.report.closed": "Closed",
  "op.cash.report.printHeader": "AFK4 · Shift report",
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json`** (после `"op.cash.shift.empty"`) — настоящий таджикский (смена = навбат):

```json
  "op.cash.action.xReport": "Ҳисоботи X",
  "op.cash.journal.tab": "Дафтари касса",
  "op.cash.journal.title": "Дафтари касса",
  "op.cash.journal.heading": "Амалиёт ва санҷиш",
  "op.cash.journal.segOps": "Амалиёти касса",
  "op.cash.journal.segReview": "Санҷиш",
  "op.cash.journal.searchPlaceholder": "Ҷустуҷӯ аз рӯи сабаб ё навъ",
  "op.cash.journal.empty": "Амалиёти касса нест",
  "op.cash.journal.noMatch": "Чизе ёфт нашуд",
  "op.cash.journal.loading": "Боркунии амалиёт…",
  "op.cash.journal.cashIn": "Воридшуда",
  "op.cash.journal.cashOut": "Хориҷшуда",
  "op.cash.journal.net": "Ҳамагӣ аз рӯи касса",
  "op.cash.journal.export": "Содирот (CSV)",
  "op.cash.report.xTitle": "Ҳисоботи X",
  "op.cash.report.zTitle": "Ҳисоботи Z",
  "op.cash.report.xSubtitle": "ҳисоботи мобайнии навбат",
  "op.cash.report.zSubtitle": "ҳисоботи ниҳоии пӯшидани навбат",
  "op.cash.report.print": "Чоп",
  "op.cash.report.revenueSection": "Даромади навбат",
  "op.cash.report.reconcileSection": "Санҷиши касса",
  "op.cash.report.opened": "Кушода шуд",
  "op.cash.report.closed": "Пӯшида шуд",
  "op.cash.report.printHeader": "AFK4 · Ҳисоботи навбат",
```

- [ ] **Step 4: Удалить ВСЕ ключи `op.payments.*` из всех трёх локалей.** Найди границы блока в каждом файле: `grep -n '"op\.payments\.' locales/ru.json` (98 ключей; так же en/tg). Удали каждую строку `"op.payments.*": "...",`. Это непрерывный блок ключей `op.payments.*` — удали его целиком в ru.json, en.json, tg.json. Убедись, что JSON остаётся валидным (нет висячих запятых, окружающие ключи целы).

- [ ] **Step 5: Регенерировать messages.ts и прогнать i18n-guard**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`
Expected: gen без ошибок (messages.ts перегенерирован — новые `op.cash.journal.*`/`op.cash.report.*` есть, `op.payments.*` исчезли); тесты PASS (включая guard tg≠ru — новые tg-значения отличаются от ru).

- [ ] **Step 6: Проверить, что фронт всё ещё собирается типами** (op.payments.* нигде не использовался — удаление ключей не должно ломать типы `MessageKey`-потребителей):

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: exit 0 (нет ссылок на удалённые ключи).

- [ ] **Step 7: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(cash-s2): ключи Журнала кассы + X/Z-отчётов (ru/en/tg); удалить 98 осиротевших op.payments.*"
```

---

### Task 2: `cash/shiftReport.ts` — данные + текст + печать X/Z

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/shiftReport.ts`
- Test: `src/AFK4.Operator.App.Web/src/cash/shiftReport.test.ts`

**Interfaces:**
- Consumes: `ShiftRevenueDto` (из `../operatorApiClients`), `formatMoney`/`formatTime`/`escapeHtml`/`readMoney`/`readString` (из `../operatorHelpers`), `MessageKey`/`t` (из `@afk4/i18n`).
- Produces:
  - `interface ShiftReportData { openedAtUtc: string; closedAtUtc: string | null; earned: {time,goods,total}; inflow: {cash,nonCash,walletTopUps}; cash: {starting,expected,counted,difference} }` (Money = `{currencyCode,minorUnits}`).
  - `buildShiftReportData(revenue: ShiftRevenueDto, closeResult?: Record<string, unknown> | null): ShiftReportData`
  - `buildShiftReportText(data: ShiftReportData, variant: 'x'|'z', currencyCode: string, t): string`
  - `printShiftReport(title: string, text: string): boolean` (window.open-печать; false если окно не открылось — happy-dom)
  Используются в Tasks 3 и 4.

- [ ] **Step 1: Написать падающий тест** `src/AFK4.Operator.App.Web/src/cash/shiftReport.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { messages } from '@afk4/i18n';
import { buildShiftReportData, buildShiftReportText } from './shiftReport';
import type { ShiftRevenueDto } from '../operatorApiClients';

const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
// мини-t без провайдера: бьём прямо в ru-словарь
const t = (key: string) => (messages.ru as Record<string, string>)[key] ?? key;

function openRevenue(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(82000), goods: m(41000), total: m(123000) },
    inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
    cash: { starting: m(100000), expected: m(190000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
  };
}

describe('buildShiftReportData', () => {
  it('X: берёт снимок открытой смены как есть (counted/difference = null)', () => {
    const data = buildShiftReportData(openRevenue());
    expect(data.cash.counted).toBeNull();
    expect(data.cash.difference).toBeNull();
    expect(data.earned.total).toEqual(m(123000));
    expect(data.closedAtUtc).toBeNull();
  });

  it('Z: накладывает counted/difference/closedAt из результата закрытия', () => {
    const data = buildShiftReportData(openRevenue(), {
      countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z'
    });
    expect(data.cash.counted).toEqual(m(185000));
    expect(data.cash.difference).toEqual(m(-5000));
    expect(data.cash.starting).toEqual(m(100000)); // из снимка
    expect(data.closedAtUtc).toBe('2026-06-24T18:00:00Z');
  });
});

describe('buildShiftReportText', () => {
  it('X-текст содержит заголовок, выручку и сверку', () => {
    const text = buildShiftReportText(buildShiftReportData(openRevenue()), 'x', 'TJS', t);
    expect(text).toContain('X-отчёт');
    expect(text).toContain('Выручка смены');
    expect(text).toContain('Сверка кассы');
    expect(text).toContain('123 с.'); // earned.total
  });

  it('Z-текст помечен как Z и показывает расхождение', () => {
    const data = buildShiftReportData(openRevenue(), { countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z' });
    const text = buildShiftReportText(data, 'z', 'TJS', t);
    expect(text).toContain('Z-отчёт');
    expect(text).toContain('-5 с.');
  });
});
```

- [ ] **Step 2: Прогнать тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/shiftReport.test.ts`
Expected: FAIL («Cannot find module './shiftReport'»).

- [ ] **Step 3: Реализовать `src/AFK4.Operator.App.Web/src/cash/shiftReport.ts`:**

```ts
import type { MessageKey } from '@afk4/i18n';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { escapeHtml, formatMoney, formatTime, readMoney } from '../operatorHelpers';

type Money = { currencyCode: string; minorUnits: number };
type TFunc = (key: MessageKey) => string;

// Данные печатной/экранной формы отчёта по смене (X = промежуточный снимок, Z = итог закрытия).
export interface ShiftReportData {
  openedAtUtc: string;
  closedAtUtc: string | null;
  earned: { time: Money; goods: Money; total: Money };
  inflow: { cash: Money; nonCash: Money; walletTopUps: Money };
  cash: { starting: Money; expected: Money; counted: Money | null; difference: Money | null };
}

// X = снимок текущей выручки/сверки как есть. Z = тот же снимок, но counted/difference/closedAt
// берём из ответа close (выручки в ответе close нет — она остаётся из снимка revenue).
export function buildShiftReportData(revenue: ShiftRevenueDto, closeResult?: Record<string, unknown> | null): ShiftReportData {
  const counted = closeResult ? readMoney(closeResult, 'countedCash') : revenue.cash.counted;
  const difference = closeResult ? readMoney(closeResult, 'difference') : revenue.cash.difference;
  const closedAtUtc = closeResult
    ? (typeof closeResult.closedAtUtc === 'string' ? closeResult.closedAtUtc : null)
    : revenue.closedAtUtc;
  return {
    openedAtUtc: revenue.openedAtUtc,
    closedAtUtc,
    earned: revenue.earned,
    inflow: revenue.inflow,
    cash: { starting: revenue.cash.starting, expected: revenue.cash.expected, counted, difference }
  };
}

// Моноширинный текст отчёта для печати (паттерн buildPosReceiptText).
export function buildShiftReportText(data: ShiftReportData, variant: 'x' | 'z', currencyCode: string, t: TFunc): string {
  const title = variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
  const row = (label: MessageKey, value: Money | null) => `${t(label)}: ${formatMoney(value, currencyCode)}`;
  const lines: string[] = [
    t('op.cash.report.printHeader'),
    title,
    `${t('op.cash.report.opened')}: ${formatTime(data.openedAtUtc)}`,
  ];
  if (data.closedAtUtc) lines.push(`${t('op.cash.report.closed')}: ${formatTime(data.closedAtUtc)}`);
  lines.push(
    '',
    t('op.cash.report.revenueSection'),
    row('op.shifts.earned', data.earned.total),
    row('op.shifts.time', data.earned.time),
    row('op.shifts.goods', data.earned.goods),
    row('op.shifts.cash', data.inflow.cash),
    row('op.shifts.nonCash', data.inflow.nonCash),
    row('op.shifts.walletTopUps', data.inflow.walletTopUps),
    '',
    t('op.cash.report.reconcileSection'),
    row('op.cash.shift.starting', data.cash.starting),
    row('op.cash.shift.expected', data.cash.expected),
    `${t('op.cash.shift.counted')}: ${data.cash.counted === null ? t('op.cash.shift.notClosed') : formatMoney(data.cash.counted, currencyCode)}`,
    `${t('op.cash.shift.difference')}: ${data.cash.difference === null ? t('op.cash.shift.notClosed') : formatMoney(data.cash.difference, currencyCode)}`
  );
  return lines.join('\n');
}

// Печать через новое окно (паттерн printSelectedReceipt). false, если окно не открылось (тесты/блокировщик).
export function printShiftReport(title: string, text: string): boolean {
  const printWindow = window.open('', '_blank', 'width=360,height=640');
  if (printWindow === null) return false;
  printWindow.document.write(`<title>${escapeHtml(title)}</title><pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(text)}</pre>`);
  printWindow.document.close();
  printWindow.focus();
  printWindow.print();
  return true;
}
```

- [ ] **Step 4: Прогнать тест — PASS**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/shiftReport.test.ts`
Expected: PASS (4 теста). Если `formatMoney` рендерит иначе чем «123 с.»/«-5 с.» — НЕ менять реализацию форматтера, подогнать строки теста под фактический вывод (формат — инвариант проекта).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/shiftReport.ts src/AFK4.Operator.App.Web/src/cash/shiftReport.test.ts
git commit -m "feat(cash-s2): билдеры данных/текста + печать X/Z-отчёта (cash/shiftReport.ts)"
```

---

### Task 3: `cash/ShiftReportModal.tsx` — презентационная форма X/Z

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.test.tsx`

**Interfaces:**
- Consumes: `ShiftReportData` (из `./shiftReport`), `PanelModal` (из `../PanelModal`), `formatMoney`/`formatTime` (из `../operatorHelpers`).
- Produces: `ShiftReportModal({ variant, data, currencyCode, onClose, onPrint })` — `variant: 'x'|'z'`, `data: ShiftReportData`, `onPrint: () => void`. Используется в Task 4.

- [ ] **Step 1: Написать падающий тест** `src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ShiftReportModal } from './ShiftReportModal';
import type { ShiftReportData } from './shiftReport';

afterEach(cleanup);
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const data: ShiftReportData = {
  openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null,
  earned: { time: m(82000), goods: m(41000), total: m(123000) },
  inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000) },
  cash: { starting: m(100000), expected: m(190000), counted: null, difference: null }
};

function renderModal(variant: 'x' | 'z', onPrint = mock(() => {})) {
  render(
    <I18nProvider initialLocale="ru">
      <ShiftReportModal variant={variant} data={data} currencyCode="TJS" onClose={() => {}} onPrint={onPrint} />
    </I18nProvider>
  );
  return onPrint;
}

describe('ShiftReportModal', () => {
  it('X-вариант: заголовок X-отчёт + выручка + сверка', () => {
    renderModal('x');
    expect(screen.getByText('X-отчёт')).toBeInTheDocument();
    expect(screen.getByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('123 с.')).toBeInTheDocument();
  });

  it('counted=null → «Смена не закрыта», не «0 с.»', () => {
    renderModal('x');
    expect(screen.getAllByText('Смена не закрыта').length).toBeGreaterThanOrEqual(2);
  });

  it('кнопка «Печать» зовёт onPrint', () => {
    const onPrint = renderModal('z');
    fireEvent.click(screen.getByRole('button', { name: 'Печать' }));
    expect(onPrint).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Прогнать — FAIL** (`Cannot find module './ShiftReportModal'`).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/ShiftReportModal.test.tsx`

- [ ] **Step 3: Реализовать `src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.tsx`:**

```tsx
import { useI18n } from '@afk4/i18n';
import { Printer } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { formatMoney, formatTime } from '../operatorHelpers';
import type { ShiftReportData } from './shiftReport';

// Презентационная форма отчёта по смене: X (промежуточный, смена открыта) или Z (итог закрытия).
// Read-only; печать — снаружи через onPrint (cash/shiftReport.printShiftReport).
export function ShiftReportModal({
  variant,
  data,
  currencyCode,
  onClose,
  onPrint
}: {
  variant: 'x' | 'z';
  data: ShiftReportData;
  currencyCode: string;
  onClose: () => void;
  onPrint: () => void;
}) {
  const { t } = useI18n();
  const title = variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
  const subtitle = variant === 'x' ? t('op.cash.report.xSubtitle') : t('op.cash.report.zSubtitle');
  const money = (value: { currencyCode: string; minorUnits: number } | null) =>
    value === null ? t('op.cash.shift.notClosed') : formatMoney(value, currencyCode);

  return (
    <PanelModal title={title} subtitle={subtitle} onClose={onClose}>
      <div className="cash-report">
        <p className="cash-report-time">
          {t('op.cash.report.opened')}: {formatTime(data.openedAtUtc)}
          {data.closedAtUtc ? ` · ${t('op.cash.report.closed')}: ${formatTime(data.closedAtUtc)}` : ''}
        </p>

        <section className="cash-report-section">
          <h3>{t('op.cash.report.revenueSection')}</h3>
          <div className="cash-shift-row"><span>{t('op.shifts.earned')}</span><strong>{money(data.earned.total)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.time')}</span><strong>{money(data.earned.time)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.goods')}</span><strong>{money(data.earned.goods)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.cash')}</span><strong>{money(data.inflow.cash)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.nonCash')}</span><strong>{money(data.inflow.nonCash)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.shifts.walletTopUps')}</span><strong>{money(data.inflow.walletTopUps)}</strong></div>
        </section>

        <section className="cash-report-section">
          <h3>{t('op.cash.report.reconcileSection')}</h3>
          <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong>{money(data.cash.starting)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong>{money(data.cash.expected)}</strong></div>
          <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{money(data.cash.counted)}</strong></div>
          <div className={`cash-shift-row${data.cash.difference !== null && data.cash.difference.minorUnits !== 0 ? ' attention' : ''}`}>
            <span>{t('op.cash.shift.difference')}</span><strong>{money(data.cash.difference)}</strong>
          </div>
        </section>

        <button type="button" className="cash-primary-action" onClick={onPrint}>
          <Printer size={15} aria-hidden="true" />
          {t('op.cash.report.print')}
        </button>
      </div>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогнать — PASS** (3 теста). Если «123 с.» не совпадёт с фактическим форматом — подогнать строку под `formatMoney`.

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/ShiftReportModal.test.tsx`

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.tsx src/AFK4.Operator.App.Web/src/cash/ShiftReportModal.test.tsx
git commit -m "feat(cash-s2): ShiftReportModal — презентационная форма X/Z-отчёта"
```

---

### Task 4: Вшить X-отчёт + Z-сводку в командную панель смены

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.test.tsx`

**Interfaces:**
- Consumes: `ShiftReportModal`, `buildShiftReportData`, `buildShiftReportText`, `printShiftReport`, `ShiftRevenueDto`.
- `CashShiftCommandBar` получает новый проп `revenue?: ShiftRevenueDto | null` (снимок текущей смены, для X и Z). `CashShiftHeader` пробрасывает `revenue={revenue}`.

- [ ] **Step 1: Расширить пропсы и логику `CashShiftCommandBar.tsx`.**

Импорты (добавить к существующим):
```tsx
import { Lock, ArrowDownToLine, ArrowUpFromLine, Unlock, FileText } from 'lucide-react';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { ShiftReportModal } from './ShiftReportModal';
import { buildShiftReportData, buildShiftReportText, printShiftReport, type ShiftReportData } from './shiftReport';
```
(замени существующую строку импорта lucide на версию с `FileText`.)

Добавь проп `revenue` в деструктуризацию и тип:
```tsx
export function CashShiftCommandBar({
  backend,
  session,
  shiftId,
  isOpen,
  expectedCash,
  currencyCode,
  revenue = null,
  onShiftChanged,
  actions: injectedActions
}: {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  shiftId: string | null;
  isOpen: boolean;
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  currencyCode: string;
  revenue?: ShiftRevenueDto | null;
  onShiftChanged: () => void;
  actions?: CashShiftActionsClient;
}) {
```

Добавь состояние отчёта (рядом с прочими useState):
```tsx
  const [report, setReport] = useState<{ variant: 'x' | 'z'; data: ShiftReportData } | null>(null);
```

Добавь право на X-отчёт (рядом с canOpen/canCash/canClose):
```tsx
  const canXReport = isOpen && hasPermission(session, permissionNames.viewReports);
```

Изменить `submitClose`, чтобы по результату закрытия собрать Z-данные и показать Z-форму. `run` отдаёт результат `fn` наружу через замыкание — соберём данные внутри `fn` до возврата:
```tsx
  const submitClose = () =>
    run(t('op.cash.action.close'), async (actions) => {
      const minor = parseNonNegativeMoneyInputMinorUnits(countedCash);
      if (minor === null || shiftId === null) throw new Error(t('op.cash.close.countedLabel'));
      const closed = await actions.closeShift(shiftId, {
        organizationId: backend!.session.organizationId,
        countedCash: { currencyCode, minorUnits: minor },
        closingNote: closingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-close')
      });
      // Z-сводка: снимок выручки (revenue) + counted/difference/closedAt из ответа close.
      if (revenue) setReport({ variant: 'z', data: buildShiftReportData(revenue, closed as Record<string, unknown>) });
    });
```

Добавь обработчик печати:
```tsx
  const printReport = () => {
    if (report === null) return;
    const title = report.variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
    printShiftReport(title, buildShiftReportText(report.data, report.variant, currencyCode, t));
  };
```

В разметке: добавь кнопку X-отчёта (после блока `canClose`, перед `feedback`):
```tsx
      {canXReport && revenue && (
        <button type="button" className="cash-command-btn" onClick={() => setReport({ variant: 'x', data: buildShiftReportData(revenue) })}>
          <FileText size={14} aria-hidden="true" />{t('op.cash.action.xReport')}
        </button>
      )}
```

И отрендерь модалку отчёта (после блока `activeModal === 'close'`, перед закрывающим `</div>`):
```tsx
      {report && (
        <ShiftReportModal
          variant={report.variant}
          data={report.data}
          currencyCode={currencyCode}
          onClose={() => setReport(null)}
          onPrint={printReport}
        />
      )}
```

> Примечание: `actions.closeShift` возвращает `Promise<unknown>`; приведение `closed as Record<string, unknown>` безопасно — `buildShiftReportData` читает поля через `readMoney`/typeof.

- [ ] **Step 2: Пробросить `revenue` из `CashShiftHeader.tsx`.** В JSX `<CashShiftCommandBar … />` добавь проп `revenue={revenue}` (переменная `revenue` уже есть в компоненте — состояние смены):

```tsx
      <CashShiftCommandBar
        backend={backend}
        session={session}
        shiftId={revenue?.shiftId ?? null}
        isOpen={header.isOpen}
        expectedCash={header.cashInHand}
        currencyCode={currencyCode}
        revenue={revenue}
        onShiftChanged={onShiftChanged}
        actions={actions}
      />
```

- [ ] **Step 3: Добавить тесты в `CashShiftCommandBar.test.tsx`** (в конец describe-блока). Сначала прочитай файл и переиспользуй его существующие фикстуры/хелперы рендера; добавь два теста — X-кнопка показывает форму, закрытие показывает Z. Образец (адаптируй под фактический хелпер рендера в файле — он передаёт `session`/`actions`/`backend`):

```tsx
  it('кнопка X-отчёт открывает форму отчёта по снимку выручки', () => {
    const revenue = {
      shiftId: 's1', organizationId: 'o', branchId: 'b1', openedByStaffUserId: 'u1', closedByStaffUserId: null,
      state: 'open',
      earned: { time: m(82000), goods: m(41000), total: m(123000) },
      inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
      cash: { starting: m(100000), expected: m(190000), counted: null, difference: null },
      openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
    } as never;
    renderBar({ isOpen: true, shiftId: 's1', expectedCash: m(190000), revenue });
    fireEvent.click(screen.getByRole('button', { name: /X-отчёт/ }));
    expect(screen.getByText('X-отчёт')).toBeInTheDocument();
    expect(screen.getByText('Выручка смены')).toBeInTheDocument();
  });

  it('успешное закрытие показывает Z-отчёт', async () => {
    const revenue = { /* как выше */ } as never;
    const actions = { openShift: mock(async () => ({})), recordCashMovement: mock(async () => ({})),
      closeShift: mock(async () => ({ countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z' })) };
    renderBar({ isOpen: true, shiftId: 's1', expectedCash: m(190000), revenue, actions });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Факт в кассе'), { target: { value: '1850.00' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Закрыть смену' }));
    expect(await screen.findByText('Z-отчёт')).toBeInTheDocument();
  });
```

> ВАЖНО для имплементера: открой `CashShiftCommandBar.test.tsx`, посмотри фактическое имя/сигнатуру хелпера рендера (в плане условно `renderBar(props)`) и фактический `m()`/импорты (`within`, `mock`). Используй ИХ. Если хелпер не принимает `revenue`/`actions` — расширь вызов рендера соответствующими пропсами по образцу существующих тестов. Не дублируй фикстуру revenue — вынеси в локальную функцию.

- [ ] **Step 4: Прогнать фокус-тест + subdir**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftCommandBar.test.tsx`
Expected: PASS (старые + 2 новых). `window.open` в happy-dom вернёт null → печать не вызывается в этих тестах (мы не кликаем «Печать»).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash`
Expected: PASS (все cash-тесты).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.test.tsx
git commit -m "feat(cash-s2): X-отчёт (кнопка в шапке) + Z-сводка после закрытия смены"
```

---

### Task 5: `cash/CashOperationsLedger.tsx` — поисковая лента кассовых операций

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.test.tsx`

**Interfaces:**
- Consumes: `getCashOperationReport`/`exportCashOperationReportCsv` (через `createAuthenticatedOperatorClients(...).shifts`), хелперы `cashOperationTypeLabel`/`formatMoney`/`formatTime`/`readArray`/`readMoney`/`readString`/`readNumber`/`downloadTextFile`.
- Produces: `CashOperationsLedger({ backend, branchId, currencyCode, shiftNonce?, reports? })` — `reports?` инъект-клиент `{ getCashOperationReport(branchId, query?) }` для тестов.

- [ ] **Step 1: Падающий тест** `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashOperationsLedger } from './CashOperationsLedger';

afterEach(cleanup);
const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function renderLedger(rows: Record<string, unknown>[]) {
  render(
    <I18nProvider initialLocale="ru">
      <CashOperationsLedger
        backend={backend}
        branchId="b1"
        currencyCode="TJS"
        reports={{ getCashOperationReport: async () => ({ rows, cashInTotal: m(5000), cashOutTotal: m(2000), netCashTotal: m(3000) }) }}
      />
    </I18nProvider>
  );
}

const rows = [
  { operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', operationType: 'cash_in', cashImpact: m(5000), reason: 'Размен кассы', sourceType: 'shift' },
  { operationId: 'c2', createdAtUtc: '2026-06-24T11:00:00Z', operationType: 'cash_out', cashImpact: m(-2000), reason: 'Инкассация', sourceType: 'shift' }
];

describe('CashOperationsLedger', () => {
  it('рендерит строки операций и сводку', async () => {
    renderLedger(rows);
    await waitFor(() => expect(screen.getByText('Размен кассы')).toBeInTheDocument());
    expect(screen.getByText('Инкассация')).toBeInTheDocument();
    expect(screen.getByText('Итого по кассе')).toBeInTheDocument();
  });

  it('поиск фильтрует по причине', async () => {
    renderLedger(rows);
    await waitFor(() => expect(screen.getByText('Размен кассы')).toBeInTheDocument());
    fireEvent.change(screen.getByPlaceholderText('Поиск по причине или типу'), { target: { value: 'инкасс' } });
    expect(screen.queryByText('Размен кассы')).toBeNull();
    expect(screen.getByText('Инкассация')).toBeInTheDocument();
  });

  it('пустой набор → пустое состояние', async () => {
    renderLedger([]);
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Прогнать — FAIL.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashOperationsLedger.test.tsx`

- [ ] **Step 3: Реализовать `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx`:**

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Download, Search } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatMoney,
  formatTime,
  readArray,
  readMoney,
  readString
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';

interface LedgerReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<Record<string, unknown>>;
}

// Поисковая лента приходно-расходных кассовых операций (cash_in/cash_out) поверх getCashOperationReport.
// Сетку методов оплаты НЕ дублируем — она в кокпите «Смена» (inflow). Действия (внести/изъять) — в шапке.
export function CashOperationsLedger({
  backend,
  branchId,
  currencyCode,
  shiftNonce = 0,
  reports: injectedReports
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  shiftNonce?: number;
  reports?: LedgerReports;
}) {
  const { t } = useI18n();
  const reports = useMemo(
    () => injectedReports ?? (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).shifts : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, injectedReports]
  );

  const [report, setReport] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  useEffect(() => {
    if (reports === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    reports.getCashOperationReport(branchId, { limit: 50 })
      .then((result) => { if (active) setReport(result); })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reports, branchId, shiftNonce]);

  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const needle = query.trim().toLowerCase();
  const filtered = needle === ''
    ? rows
    : rows.filter((row) => {
        const type = cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t).toLowerCase();
        return readString(row, 'reason').toLowerCase().includes(needle) || type.includes(needle);
      });

  const exportCsv = async () => {
    if (backend === null) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    downloadTextFile(`afk4-cash-operations-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 200 }), 'text/csv;charset=utf-8');
  };

  if (loading) return <p className="workspace-loading">{t('op.cash.journal.loading')}</p>;
  if (loadError) return <p className="workspace-error" role="alert">{loadError}</p>;

  return (
    <section className="cash-ledger">
      <div className="cash-ledger-summary">
        <span><b>{t('op.cash.journal.cashIn')}</b> {formatMoney(readMoney(report, 'cashInTotal'), currencyCode)}</span>
        <span><b>{t('op.cash.journal.cashOut')}</b> {formatMoney(readMoney(report, 'cashOutTotal'), currencyCode)}</span>
        <span><b>{t('op.cash.journal.net')}</b> {formatMoney(readMoney(report, 'netCashTotal'), currencyCode)}</span>
      </div>
      <div className="cash-ledger-search">
        <Search size={14} aria-hidden="true" />
        <input
          value={query}
          onChange={(event) => setQuery(event.currentTarget.value)}
          placeholder={t('op.cash.journal.searchPlaceholder')}
          aria-label={t('op.cash.journal.searchPlaceholder')}
        />
        <button type="button" className="cash-ledger-export" onClick={() => void exportCsv()}>
          <Download size={14} aria-hidden="true" />{t('op.cash.journal.export')}
        </button>
      </div>
      {filtered.length === 0 ? (
        <p className="cash-shift-empty-note">{rows.length === 0 ? t('op.cash.journal.empty') : t('op.cash.journal.noMatch')}</p>
      ) : (
        <ul className="cash-ledger-list">
          {filtered.map((row) => {
            const impact = readMoney(row, 'cashImpact');
            const negative = impact !== null && impact.minorUnits < 0;
            return (
              <li key={readString(row, 'operationId')} className={`cash-ledger-row${negative ? ' out' : ' in'}`}>
                <span className="cash-ledger-time">{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</strong>
                <em>{readString(row, 'reason')}</em>
                <b>{formatMoney(impact, currencyCode)}</b>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Прогнать — PASS** (3 теста).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashOperationsLedger.test.tsx`

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.test.tsx
git commit -m "feat(cash-s2): CashOperationsLedger — поисковая лента кассовых операций"
```

---

### Task 6: `ReviewWorkspace` встроенный режим + `cash/CashJournalWorkspace.tsx`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx` (добавить `embedded`)
- Create: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx`

**Interfaces:**
- Consumes: `CashOperationsLedger`, `ReviewWorkspace`, `hasAnyPermission`/`permissionNames`, `OperatorAuthSession`, `OperatorBackendContext`.
- Produces: `CashJournalWorkspace({ backend, currencyCode, session })`. `ReviewWorkspace` получает `embedded?: boolean`.

- [ ] **Step 1: Добавить `embedded` в `ReviewWorkspace.tsx`.** Расширь сигнатуру:

```tsx
export function ReviewWorkspace({ currencyCode, backend, embedded = false }: { currencyCode: string; backend: OperatorBackendContext | null; embedded?: boolean }) {
```

В `return` замени корневой `<main className="workspace-screen review-screen">` на условную обёртку и скрой собственный `screen-head` во встроенном режиме. Конкретно — оберни весь текущий контент в переменную и отрендерь:

```tsx
  const body = (
    <>
      {!embedded && (
        <section className="screen-head review-head">
          <div>
            <span>{t('op.review.title')}</span>
            <h1>{t('op.review.heading')}</h1>
          </div>
          <div className="screen-actions">
            <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.review.loadedLabel'), t)}</span>
          </div>
        </section>
      )}
      {/* далее без изменений: state-strip, review-segments, queue-panel, audit-panel */}
      <section className="state-strip review-state-strip" aria-label={t('op.review.summaryLabel')}>
        ...
      </section>
      <div className="review-segments" role="tablist">
        ...
      </div>
      {activeSegment === 'queue' && ( ... )}
      {activeSegment === 'audit' && ( ... )}
    </>
  );
  return embedded ? <section className="review-embed">{body}</section> : <main className="workspace-screen review-screen">{body}</main>;
```

> Имплементер: сохрани ВЕСЬ существующий контент (state-strip, segments, обе панели queue/audit, FeedbackNotice) без логических изменений — меняется только корневая обёртка и условный показ `screen-head`. Не трогай загрузку/approve/reject/audit.

- [ ] **Step 2: Падающий тест** `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashJournalWorkspace } from './CashJournalWorkspace';

afterEach(cleanup);

// backend=null → компоненты не строят боевой клиент; сегменты гейтятся правами session.
function renderJournal(permissions: string[]) {
  const session = { permissions, organizationId: 'o' } as never;
  render(
    <I18nProvider initialLocale="ru">
      <CashJournalWorkspace backend={null} currencyCode="TJS" session={session} />
    </I18nProvider>
  );
}

describe('CashJournalWorkspace', () => {
  it('менеджер видит оба сегмента, по умолчанию активны «Кассовые операции»', async () => {
    renderJournal(['reports.view', 'money.action.approve']);
    expect(screen.getByRole('tab', { name: 'Кассовые операции' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Проверка' })).toBeInTheDocument();
  });

  it('без права approve сегмент «Проверка» скрыт', () => {
    renderJournal(['reports.view']);
    expect(screen.getByRole('tab', { name: 'Кассовые операции' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Проверка' })).toBeNull();
  });

  it('переключение на «Проверка» показывает очередь возвратов', () => {
    renderJournal(['reports.view', 'money.action.approve']);
    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    // ReviewWorkspace во встроенном режиме рендерит свои внутренние сегменты
    expect(screen.getByRole('tablist')).toBeInTheDocument();
  });
});
```

> Имплементер: точные строки прав — из `permissionNames` (`reports.view` = viewReports, `money.action.approve` = approveMoneyAction). Сверься с `operatorPermissions.ts`; если значения иные — подставь фактические.

- [ ] **Step 3: Прогнать — FAIL.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashJournalWorkspace.test.tsx`

- [ ] **Step 4: Реализовать `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx`:**

```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { CashOperationsLedger } from './CashOperationsLedger';
import { ReviewWorkspace } from '../ReviewWorkspace';

type JournalSegment = 'ops' | 'review';

// Вкладка «Журнал кассы» = лента кассовых операций + аппрув возвратов/коррекций (ReviewWorkspace
// во встроенном режиме). Сегменты гейтятся правами — оператор не видит сегмент без доступа.
export function CashJournalWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const canOps = hasAnyPermission(session, [permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash]);
  const canReview = hasAnyPermission(session, [permissionNames.approveMoneyAction]);

  const segments: { id: JournalSegment; label: string }[] = [];
  if (canOps) segments.push({ id: 'ops', label: t('op.cash.journal.segOps') });
  if (canReview) segments.push({ id: 'review', label: t('op.cash.journal.segReview') });

  const [active, setActive] = useState<JournalSegment>(() => segments[0]?.id ?? 'ops');

  return (
    <main className="workspace-screen cash-journal-screen">
      <section className="screen-head">
        <div>
          <span>{t('op.cash.journal.title')}</span>
          <h1>{t('op.cash.journal.heading')}</h1>
        </div>
      </section>

      {segments.length > 1 && (
        <div className="cash-journal-segments" role="tablist" aria-label={t('op.cash.journal.title')}>
          {segments.map((segment) => (
            <button
              key={segment.id}
              type="button"
              role="tab"
              aria-selected={active === segment.id}
              className={active === segment.id ? 'active' : undefined}
              onClick={() => setActive(segment.id)}
            >
              {segment.label}
            </button>
          ))}
        </div>
      )}

      {active === 'ops' && canOps && backend !== null && (
        <CashOperationsLedger backend={backend} branchId={backend.branchId} currencyCode={currencyCode} />
      )}
      {active === 'ops' && canOps && backend === null && (
        <CashOperationsLedger backend={null} branchId="" currencyCode={currencyCode} reports={{ getCashOperationReport: async () => ({ rows: [] }) }} />
      )}
      {active === 'review' && canReview && <ReviewWorkspace currencyCode={currencyCode} backend={backend} embedded />}
    </main>
  );
}
```

> Примечание: при `backend === null` (тесты/без бэка) лента получает инъект-репортс с пустыми строками, чтобы не строить боевой клиент. В реальном приложении `backend !== null`.

- [ ] **Step 5: Прогнать фокус-тест + cash subdir**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashJournalWorkspace.test.tsx`
Expected: PASS.

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash`
Expected: PASS (все cash-тесты).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx
git commit -m "feat(cash-s2): CashJournalWorkspace (операции + проверка) + встроенный режим ReviewWorkspace"
```

---

### Task 7: Перевести вкладку review → journal («Журнал кассы»)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts`

**Interfaces:**
- `CashTab` union: `'sales' | 'orders' | 'shift' | 'journal'`.
- `CASH_TAB_PERMISSIONS.journal` = `[approveMoneyAction, viewReports, viewShift, manageShiftCash]` (суперсет: вкладка видна, если есть доступ хотя бы к одному сегменту — без регрессии доступа). `CASH_TAB_ORDER` = `['sales','orders','shift','journal']`.

- [ ] **Step 1: `CashTabBar.tsx`** — заменить `'review'` на `'journal'` в union:

```tsx
export type CashTab = 'sales' | 'orders' | 'shift' | 'journal';
```

- [ ] **Step 2: `cashModel.ts`** — заменить ключ `review` на `journal` в `CASH_TAB_PERMISSIONS` и `CASH_TAB_ORDER`:

```ts
const CASH_TAB_PERMISSIONS: Record<CashTab, readonly string[]> = {
  sales: [permissionNames.createPosSale, permissionNames.payPosSale, permissionNames.refundPosSale, permissionNames.voidPosSale],
  orders: [permissionNames.createPosSale],
  shift: [permissionNames.viewShift, permissionNames.openShift, permissionNames.closeShift, permissionNames.manageShiftCash, permissionNames.viewReports],
  journal: [permissionNames.approveMoneyAction, permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash]
};

const CASH_TAB_ORDER: CashTab[] = ['sales', 'orders', 'shift', 'journal'];
```

- [ ] **Step 3: `CashWorkspace.tsx`** — импорт, ярлык и рендер. Замени импорт `ReviewWorkspace` на `CashJournalWorkspace`:

```tsx
import { CashJournalWorkspace } from './CashJournalWorkspace';
```
(убери `import { ReviewWorkspace } from '../ReviewWorkspace';`)

В `allTabs` замени запись review:
```tsx
    { id: 'journal', label: t('op.cash.journal.tab') }
```

В рендере вкладок замени строку review:
```tsx
        {activeTab === 'journal' && <CashJournalWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
```

- [ ] **Step 4: Обновить `cashModel.test.ts`** — заменить упоминания `review` на `journal`. Прочитай файл; в тестах прав/порядка вкладок поменяй `'review'` → `'journal'` и ожидаемые права на новый суперсет. Если тест проверяет, что вкладка видна по `approveMoneyAction` — оставь (право в суперсете); добавь проверку, что `journal` виден и по `viewReports`.

- [ ] **Step 5: Прогнать cash subdir + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash`
Expected: PASS.

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: exit 0 (union/типы согласованы; App.test ломается в РАНТАЙМЕ, не тайпчеке — мигрируется Task 8).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx src/AFK4.Operator.App.Web/src/cash/cashModel.ts src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts
git commit -m "refactor(cash-s2): вкладка review → journal «Журнал кассы» (union/права/рендер)"
```

---

### Task 8: CSS «Журнала кассы» + формы отчёта

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:** только стили; классы из Tasks 3/5/6 (`cash-journal-screen`, `cash-journal-segments`, `cash-ledger*`, `cash-report*`, `review-embed`).

- [ ] **Step 1: Дописать в конец `21-cash.css`** (переиспуем токены и существующие `.cash-shift-row`):

```css
/* S2: вкладка «Журнал кассы» — сегменты, лента операций, встроенная проверка. */
.cash-journal-segments {
  display: flex;
  gap: var(--space-2);
  margin-bottom: var(--space-2);
  border-bottom: 1px solid var(--border-soft);
}
.cash-journal-segments button {
  appearance: none;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  padding: var(--space-1) var(--space-2);
  min-height: var(--control-sm);
  color: var(--text-secondary);
  font-size: 12px;
  cursor: pointer;
}
.cash-journal-segments button:hover { color: var(--text-primary); }
.cash-journal-segments button.active { color: var(--accent-bright); border-bottom-color: var(--accent); }
.cash-journal-segments button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.review-embed { display: block; }

.cash-ledger { display: flex; flex-direction: column; gap: var(--space-2); }
.cash-ledger-summary { display: flex; flex-wrap: wrap; gap: var(--space-3); font-size: 12px; color: var(--text-secondary); }
.cash-ledger-summary b { color: var(--text-primary); font-weight: 600; }
.cash-ledger-search { display: flex; align-items: center; gap: var(--space-2); }
.cash-ledger-search input {
  flex: 1;
  min-width: 0;
  min-height: var(--control-md);
  padding: 0 var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: 6px;
  background: var(--surface-base, var(--surface-elevated));
  color: var(--text-primary);
}
.cash-ledger-export {
  display: inline-flex; align-items: center; gap: var(--space-1);
  min-height: var(--control-md); padding: 0 var(--space-2);
  border: 1px solid var(--border-soft); border-radius: 6px;
  background: var(--surface-elevated); color: var(--text-primary); cursor: pointer;
}
.cash-ledger-export:hover { border-color: var(--accent); }
.cash-ledger-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-1); }
.cash-ledger-row {
  display: grid;
  grid-template-columns: auto auto 1fr auto;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-1) var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: 6px;
  background: var(--surface-elevated);
  font-size: 12px;
}
.cash-ledger-row.out b { color: var(--danger, #d3403a); }
.cash-ledger-time { color: var(--text-secondary); }
.cash-ledger-row em { color: var(--text-secondary); font-style: normal; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* S2: форма X/Z-отчёта в модалке. */
.cash-report { display: flex; flex-direction: column; gap: var(--space-3); }
.cash-report-time { margin: 0; font-size: 12px; color: var(--text-secondary); }
.cash-report-section { display: flex; flex-direction: column; gap: var(--space-1); }
.cash-report-section h3 { margin: 0 0 var(--space-1); font-size: 13px; color: var(--text-secondary); font-weight: 600; }
```

- [ ] **Step 2: Проверить сборку стилей (через build)**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: exit 0.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/21-cash.css
git commit -m "style(cash-s2): стили «Журнала кассы» (сегменты/лента) + формы X/Z-отчёта"
```

---

### Task 9: App.test — миграция «Проверки» в «Журнал кассы» + покрытие X/Z

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

**Interfaces:** интеграционные тесты под новую IA. Моки `/shifts/revenue/current`, `/reports/cash-operations`, `/money-actions` уже есть в `mockPlatformFetch`.

- [ ] **Step 1: `TAB_SECTION`** (≈ строка 10) — заменить `'Проверка': 'Касса'` на `'Журнал кассы': 'Касса'`:

```ts
const TAB_SECTION: Record<string, string> = {
  'Продажи': 'Касса', 'Смена': 'Касса', 'Журнал кассы': 'Касса',
  'Дашборд': 'Отчёты',
  'Настройки': 'Управление', 'Приём платежей': 'Управление', 'Лояльность': 'Управление', 'Новости': 'Управление', 'Логи': 'Управление'
};
```

- [ ] **Step 2: Тест «hides the review tab without the approve permission»** (≈ 2880).

Сейчас сессия имеет только `['pos.sales.create','pos.sales.pay']` → нет `viewReports/viewShift/manageShiftCash/approveMoneyAction` → вкладка «Журнал кассы» скрыта. Замени проверку отсутствия `'Проверка'` на `'Журнал кассы'`:

```tsx
    expect(within(strip).getByRole('tab', { name: 'Продажи' })).toBeInTheDocument();
    expect(within(strip).queryByRole('tab', { name: 'Журнал кассы' })).toBeNull();
```
(переименуй `it(...)` в `'hides the cash journal tab without cash/review permissions'`).

- [ ] **Step 3: Тест «opens the review workspace for a manager»** (≈ 2892):

```tsx
  it('opens the cash journal for a manager', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });

    gotoWorkspace('Журнал кассы');
    expect(await screen.findByRole('heading', { name: /Операции и проверка/ })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    expect(await screen.findByText('Клиент отменил заказ')).toBeInTheDocument();
  });
```

- [ ] **Step 4: Тесты approve / reject** (≈ 2901, 2920) — после `gotoWorkspace('Журнал кассы')` добавить клик по сегменту «Проверка», затем существующие ассерты без изменений. Пример для approve:

```tsx
    gotoWorkspace('Журнал кассы');
    fireEvent.click(await screen.findByRole('tab', { name: 'Проверка' }));
    expect(await screen.findByText('Клиент отменил заказ')).toBeInTheDocument();
    ...
```
Аналогично в reject-тесте (после `gotoWorkspace('Журнал кассы')` → клик «Проверка» → `await screen.findByText('Клиент отменил заказ')`).

- [ ] **Step 5: Тест audit** (≈ 2943) — навигация: `gotoWorkspace('Журнал кассы')` → клик сегмента «Проверка» → `await findByText('Клиент отменил заказ')` → клик внутреннего таба «Журнал операций» → далее без изменений.

- [ ] **Step 6: Новый тест — лента кассовых операций в «Журнале кассы»** (добавить в тот же describe):

```tsx
  it('shows the cash operations ledger in the cash journal', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });

    gotoWorkspace('Журнал кассы');
    // По умолчанию активны «Кассовые операции» (первый сегмент)
    expect(await screen.findByText('Итого по кассе')).toBeInTheDocument();
  });
```
> Сегмент «Кассовые операции» дёргает `/reports/cash-operations` (мок `createCashReport()` уже отдаёт `cashInTotal/cashOutTotal/netCashTotal`). Если `createCashReport()` не содержит этих агрегатов — добавь их (по форме отчёта). Проверь фактический набор полей фикстуры (≈ 4085).

- [ ] **Step 7: Новый тест — X-отчёт из шапки** (вкладка «Смена», смена открыта):

```tsx
  it('opens the X report from the cash header', async () => {
    installSessionBridge();
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });
    gotoWorkspace('Смена');

    fireEvent.click(await screen.findByRole('button', { name: /X-отчёт/ }));
    expect(await screen.findByText('X-отчёт')).toBeInTheDocument();
    expect(screen.getByText('Выручка смены')).toBeInTheDocument();
  });
```
> Дефолтная сессия (`installSessionBridge()`) имеет право `reports.view` (та же, что у кокпита «Смена»). Если нет — добавь его в дефолтные права фикстуры сессии (сверься, какие права даёт `installSessionBridge()`/`createSession()` по умолчанию — кокпит «Смена» уже открывается, значит viewShift есть; для X нужен viewReports).

- [ ] **Step 8: Расширить тест закрытия смены — показ Z-отчёта** (≈ 1668, «closes the current shift…»). После `expect(await screen.findByText('Закрыть смену: подтверждено'))` и проверки POST `/close`, добавь в конец теста дренаж второй волны + проверку Z-формы. ВАЖНО: действие закрытия бампает `shiftNonce` → вторая волна рефетчей шапки/кокпита; дренируй её (счётчик `/shifts/revenue/current`), иначе флак:

```tsx
    // Z-сводка показана по результату закрытия.
    expect(await screen.findByText('Z-отчёт')).toBeInTheDocument();
    // Дренаж второй волны рефетчей (onShiftChanged → shiftNonce++), чтобы async не утёк в соседние тесты.
    await waitFor(() => {
      const after = fetchMock.mock.calls.filter(([input]) => String(input).includes('/shifts/revenue/current')).length;
      expect(after).toBeGreaterThanOrEqual(revenueCallsBeforeClose + 2);
    });
    await act(async () => { await Promise.resolve(); });
```
> `revenueCallsBeforeClose` уже объявлена в тесте (строка ≈ 1686). Убедись, что `act` импортирован (он используется в других тестах файла). Если close-мок `createClosedShift` не содержит `countedCash/difference` — он уже их содержит (проверено, ≈ 3943).

- [ ] **Step 9: Прогнать App.test ОТДЕЛЬНО (несколько раз — ловим флак)**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Затем повтори ещё 2 раза. Expected: PASS на всех прогонах, без «Unhandled error between tests». Если перемежающийся флак — усиль дренаж второй волны в новых/изменённых тестах действий (см. Global Constraints).

- [ ] **Step 10: Полный фронт-гейт + build + i18n**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
Expected: subdir PASS && App.test PASS.

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: exit 0.

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`
Expected: PASS (tg≠ru).

- [ ] **Step 11: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "test(cash-s2): миграция «Проверки» в «Журнал кассы» + покрытие ленты/X/Z"
```

---

## Self-Review (заполнено при написании плана)

- **Spec coverage:** (1) приходно-расходный ордер UI + лента → модалки сделаны в S1, лента = Task 5 (CashOperationsLedger) в Task 6 (журнал). (2) X-отчёт read-only из current revenue → Task 4 (кнопка) + Task 3 (форма) + Task 2 (данные). (3) Z-сводка печатная при закрытии → Task 4 (показ после close) + Task 2 (печать). Чистка op.payments.* → Task 1. Вкладка «Журнал кассы» = cash-movements + аппрув (spec-true, подтверждено пользователем) → Tasks 5–7.
- **Tab rename влияет на App.test** — учтено в Task 9 (5 мигрируемых тестов + TAB_SECTION).
- **Деньги/null-плейсхолдер** — `money()`/`notClosed` в ShiftReportModal и тексте отчёта (Tasks 2–3), не «0 с.».
- **Флак-дренаж** — Task 9 Step 8 (закрытие бампает nonce).
- **Тип-консистентность:** `ShiftReportData` определён в Task 2, потребляется в Tasks 3–4; `CashTab` обновлён в Task 7 синхронно в CashTabBar+cashModel+CashWorkspace.
- **Боевой клиент только при backend && !injected** — CashOperationsLedger (Task 5), CashJournalWorkspace null-ветка (Task 6).
- **YAGNI:** без сетки методов (дубль кокпита), без печатного КО-документа, без новых бэк-сущностей, без X/Z как entity.
