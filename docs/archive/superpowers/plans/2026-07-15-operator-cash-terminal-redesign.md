# Operator Cash Terminal Redesign Implementation Plan

**Status:** Implemented and verified on `feat/operator-cash-terminal-redesign` on 2026-07-15. The archived checklist below preserves the execution recipe; final evidence is summarized in `docs/progress/2026-05-12-vertical-slice-progress.md` and `design-qa.md`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Operator App shift, cash-operation, receipt, approval, and audit surfaces as a cohesive dense cash terminal with stable master-detail workflows.

**Architecture:** Keep the existing `CashWorkspace`, typed clients, authoritative commands, and permission boundaries. Add focused cash-terminal view models and shared layout primitives, then migrate each state independently through a red-green component cycle. Production APIs remain unchanged; development fixtures gain contract-faithful receipt, approval, and audit detail.

**Tech Stack:** React 19, TypeScript 6, Bun test, Testing Library, Vite 8, existing AFK4 tokens/i18n/Lucide stack, Playwright with system Chromium.

## Global Constraints

- Work only in `.worktrees/operator-cash-terminal-redesign` on `feat/operator-cash-terminal-redesign`.
- Preserve backend contracts, idempotency, authoritative money responses, and critical confirmations.
- Add no charting or UI dependency; keep the `Sales` workflow out of scope.
- Hide inaccessible tabs and segments; navigation visibility is not authorization.
- Validate exactly `1440x900` and `1280x720`; mobile-phone layout is out of scope.
- Edit `locales/{ru,en,tg}.json`, then run `bun run gen` in `packages/i18n`.
- Observe each focused test fail for the intended reason before production changes.
- Baseline at `7958a60c`: 666 component/model plus 87 App tests pass (753 total). Existing `act`, duplicate-key, and test-harness `ECONNREFUSED` warnings are non-failing baseline noise.

## File Structure

- `cash/cashModel.ts` — top-level tab permission projection.
- `cash/cashTerminalModel.ts` — pure segment, summary, filter, and selection projections.
- `cash/CashTerminalFrame.tsx` — metric strip, register/inspector split, keyboard rows.
- `cash/CashShiftWorkspace.tsx` — current shift and selectable shift history.
- `cash/CashOperationsLedger.tsx` — cash-operation register and inspector.
- `cash/CashReceiptsLedger.tsx` — receipt register, detail, and refund flow.
- `cash/CashJournalWorkspace.tsx` — permission-derived segment routing.
- `ReviewWorkspace.tsx` — pending decisions, decision history, and audit.
- `devMockBackend.ts` — contract-faithful preview fixtures.
- `styles/21-cash.css`, `styles/18-review.css` — terminal and approval presentation.

---

### Task 1: Cash Terminal Models And Receipt-Only Access

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx`

**Interfaces:**
- Produces `CashJournalSegment = 'ops' | 'receipts' | 'review'`.
- Produces `visibleCashJournalSegments(session)`, `filterCashOperationRows(rows, query, type)`, and `resolveRegisterSelection(rows, selectedId, idKey)`.

- [ ] **Step 1: Write failing model tests**

```ts
it('receipt-only staff can reach only journal receipts', () => {
  const s = session(['receipts.view']);
  expect(visibleCashTabs(s)).toEqual(['journal']);
  expect(visibleCashJournalSegments(s)).toEqual(['receipts']);
});

it('keeps valid selection and otherwise selects the first row', () => {
  const rows = [{ operationId: 'a' }, { operationId: 'b' }];
  expect(resolveRegisterSelection(rows, 'b', 'operationId')).toBe('b');
  expect(resolveRegisterSelection(rows, 'missing', 'operationId')).toBe('a');
  expect(resolveRegisterSelection([], 'a', 'operationId')).toBe('');
});

it('filters operations by query and exact type', () => {
  const rows = [
    { operationId: 'a', operationType: 'cash_in', reason: 'Разменный фонд' },
    { operationId: 'b', operationType: 'cash_out', reason: 'Инкассация' }
  ];
  expect(filterCashOperationRows(rows, 'размен', 'cash_in')).toEqual([rows[0]]);
});
```

- [ ] **Step 2: Verify red**

Run from `src/AFK4.Operator.App.Web`:

```bash
bun test src/cash/cashModel.test.ts src/cash/cashTerminalModel.test.ts src/cash/CashJournalWorkspace.test.tsx
```

Expected: FAIL because the new exports do not exist and receipt permission does not expose `journal`.

- [ ] **Step 3: Implement pure helpers and centralized permissions**

```ts
export type CashJournalSegment = 'ops' | 'receipts' | 'review';

export function visibleCashJournalSegments(session: OperatorAuthSession | null): CashJournalSegment[] {
  const result: CashJournalSegment[] = [];
  if (hasAnyPermission(session, [permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash])) result.push('ops');
  if (hasAnyPermission(session, [permissionNames.viewReceipt, permissionNames.refundPosSale])) result.push('receipts');
  if (hasPermission(session, permissionNames.approveMoneyAction)) result.push('review');
  return result;
}

export function resolveRegisterSelection(rows: Record<string, unknown>[], selectedId: string, idKey: string): string {
  if (rows.some((row) => readString(row, idKey) === selectedId)) return selectedId;
  return rows.length === 0 ? '' : readString(rows[0], idKey);
}

export function filterCashOperationRows(rows: Record<string, unknown>[], query: string, operationType: string) {
  const needle = query.trim().toLocaleLowerCase();
  return rows.filter((row) => {
    const type = readString(row, 'operationType');
    return (operationType === 'all' || type === operationType)
      && (needle === '' || `${type} ${readString(row, 'reason')}`.toLocaleLowerCase().includes(needle));
  });
}
```

Add `viewReceipt` and `refundPosSale` to `CASH_TAB_PERMISSIONS.journal`. Make `CashJournalWorkspace` use `visibleCashJournalSegments` instead of duplicating permission lists.

- [ ] **Step 4: Verify green and commit**

```bash
bun test src/cash/cashModel.test.ts src/cash/cashTerminalModel.test.ts src/cash/CashJournalWorkspace.test.tsx
git add src/AFK4.Operator.App.Web/src/cash
git commit -m "feat(operator-cash): centralize terminal view models"
```

Expected: focused tests PASS.

---

### Task 2: Shared Metric, Register, And Inspector Frame

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashTerminalFrame.tsx`
- Create: `src/AFK4.Operator.App.Web/src/cash/CashTerminalFrame.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:**
- Produces `CashMetricStrip({ items })`.
- Produces `CashTerminalSplit({ register, inspector, inspectorOpen, onCloseInspector })`.
- Produces generic `CashRegisterRows({ rows, selectedId, getId, renderRow, onSelect, ariaLabel })`.

- [ ] **Step 1: Write failing semantic and keyboard tests**

```tsx
it('selects a stable row and moves with ArrowDown', () => {
  const onSelect = mock();
  render(<CashRegisterRows rows={[{ id: 'a' }, { id: 'b' }]} selectedId="a" getId={(row) => row.id} renderRow={(row) => <span>{row.id}</span>} onSelect={onSelect} ariaLabel="Операции" />);
  const first = screen.getByRole('row', { name: 'a' });
  first.focus();
  fireEvent.keyDown(first, { key: 'ArrowDown' });
  expect(onSelect).toHaveBeenCalledWith('b');
  expect(first).toHaveAttribute('aria-selected', 'true');
});

it('closes the responsive inspector explicitly', () => {
  const onClose = mock();
  render(<CashTerminalSplit register={<div>Реестр</div>} inspector={<div>Деталь</div>} inspectorOpen onCloseInspector={onClose} />);
  fireEvent.click(screen.getByRole('button', { name: 'Закрыть детали' }));
  expect(onClose).toHaveBeenCalledTimes(1);
});
```

- [ ] **Step 2: Verify red**

```bash
bun test src/cash/CashTerminalFrame.test.tsx
```

Expected: FAIL because the component does not exist.

- [ ] **Step 3: Implement the shared frame**

```tsx
export function CashMetricStrip({ items }: { items: CashMetricItem[] }) {
  return <section className="cash-terminal-metrics" aria-label="Ключевые показатели">
    {items.map((item) => <div key={item.label} className={`cash-terminal-metric tone-${item.tone ?? 'default'}`}>
      <span>{item.label}</span><strong>{item.value}</strong>{item.detail ? <small>{item.detail}</small> : null}
    </div>)}
  </section>;
}

export function CashTerminalSplit(props: CashTerminalSplitProps) {
  return <div className={`cash-terminal-split${props.inspectorOpen ? ' inspector-open' : ''}`}>
    <section className="cash-terminal-register">{props.register}</section>
    <aside className="cash-terminal-inspector" aria-label="Детали выбранной записи">
      <button type="button" className="cash-inspector-close" aria-label="Закрыть детали" onClick={props.onCloseInspector}><X size={16} /></button>
      {props.inspector}
    </aside>
  </div>;
}
```

Implement rows with `role="row"`, `tabIndex`, `aria-selected`, and immutable ArrowUp/ArrowDown/Home/End selection. Add a 64/36 grid, stable 44px rows, focus inset, and no hover transform.

- [ ] **Step 4: Verify green and commit**

```bash
bun test src/cash/CashTerminalFrame.test.tsx
git add src/AFK4.Operator.App.Web/src/cash/CashTerminalFrame.tsx src/AFK4.Operator.App.Web/src/cash/CashTerminalFrame.test.tsx src/AFK4.Operator.App.Web/src/styles/21-cash.css
git commit -m "feat(operator-cash): add terminal register frame"
```

---

### Task 3: Rebuild The Shift Cockpit

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `locales/{ru,en,tg}.json`
- Regenerate: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes the shared terminal frame.
- Adds pure shift summary/reconciliation projection and a selected history inspector.

- [ ] **Step 1: Write failing cockpit tests**

```tsx
it('renders the terminal metrics and reconciliation formula', async () => {
  renderWs(openShift(), [{ operationId: 'c1', operationType: 'cash_in', reason: 'Размен', cashImpact: m(5000), createdAtUtc: '2026-06-24T10:00:00Z' }]);
  expect(await screen.findByLabelText('Ключевые показатели смены')).toBeInTheDocument();
  expect(screen.getByText('Старт + наличные продажи + внесения − изъятия − возвраты')).toBeInTheDocument();
  expect(screen.getByText('Размен')).toBeInTheDocument();
});

it('selects closed history into a read-only inspector', async () => {
  renderWsWithHistory([closedShift]);
  fireEvent.click(await screen.findByRole('row', { name: /20\.05\.2026/ }));
  expect(screen.getByLabelText('Детали выбранной записи')).toHaveTextContent('2 340');
});

it('shows the last closed shift when none is open', async () => {
  renderWsWithHistory([closedShift], null);
  expect(await screen.findByText('Сейчас нет открытой смены')).toBeInTheDocument();
  expect(screen.getByText('Последняя закрытая смена')).toBeInTheDocument();
});
```

- [ ] **Step 2: Verify red**

```bash
bun test src/cash/cashTerminalModel.test.ts src/cash/CashShiftWorkspace.test.tsx
```

Expected: FAIL for missing terminal strip, formula, selectable history, and last-close state.

- [ ] **Step 3: Implement the 60/40 cockpit and history split**

```tsx
<CashMetricStrip items={summaryItems} />
<div className="cash-shift-primary-grid">
  <ShiftRevenueBreakdown shift={current} currencyCode={currencyCode} />
  <ShiftReconciliation shift={current} currencyCode={currencyCode} />
</div>
<CashTerminalSplit
  inspectorOpen={selectedHistoryId !== ''}
  onCloseInspector={() => setSelectedHistoryId('')}
  register={<ShiftHistoryRegister shifts={history} selectedId={selectedHistoryId} onSelect={setSelectedHistoryId} currencyCode={currencyCode} />}
  inspector={<ShiftHistoryInspector shift={selectedHistory} currencyCode={currencyCode} onExport={() => void exportCsv('shifts')} />}
/>
```

Keep helper components at module scope. Preserve the existing parallel loads, money formatting, and CSV clients. Replace the export card with a compact action cluster.

- [ ] **Step 4: Regenerate i18n, verify, and commit**

```bash
cd packages/i18n && bun run gen && bun test src/messages.test.ts
cd ../../src/AFK4.Operator.App.Web
bun test src/cash/cashTerminalModel.test.ts src/cash/CashShiftWorkspace.test.tsx
bunx tsc -b --pretty false
cd ../../..
git add src/AFK4.Operator.App.Web/src/cash src/AFK4.Operator.App.Web/src/styles/21-cash.css locales packages/i18n/src/messages.ts
git commit -m "feat(operator-cash): rebuild shift cockpit"
```

Expected: focused tests and typecheck PASS.

---

### Task 4: Convert Cash Operations To Master-Detail

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `locales/{ru,en,tg}.json`
- Regenerate: `packages/i18n/src/messages.ts`

- [ ] **Step 1: Write failing register tests**

```tsx
it('filters by type and reports visible results', async () => {
  renderLedger(rows);
  await screen.findByText('Размен кассы');
  fireEvent.change(screen.getByLabelText('Тип операции'), { target: { value: 'cash_out' } });
  expect(screen.queryByText('Размен кассы')).toBeNull();
  expect(screen.getByText('1 операция')).toBeInTheDocument();
});

it('shows selected context in the stable inspector', async () => {
  renderLedger(rows);
  fireEvent.click(await screen.findByRole('row', { name: /Инкассация/ }));
  const inspector = screen.getByLabelText('Детали выбранной записи');
  expect(inspector).toHaveTextContent('Инкассация');
  expect(inspector).toHaveTextContent('c2');
});

it('offers local retry after report failure', async () => {
  renderFailingLedger();
  expect(await screen.findByRole('alert')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
});
```

- [ ] **Step 2: Verify red**

```bash
bun test src/cash/CashOperationsLedger.test.tsx
```

Expected: FAIL for missing type filter, count, inspector, and retry.

- [ ] **Step 3: Implement toolbar, selection, inspector, and retry**

```tsx
const filtered = filterCashOperationRows(rows, query, operationType);
const effectiveId = resolveRegisterSelection(filtered, selectedId, 'operationId');
const selected = filtered.find((row) => readString(row, 'operationId') === effectiveId) ?? null;

<CashTerminalSplit
  inspectorOpen={selected !== null}
  onCloseInspector={() => setSelectedId('')}
  register={<><CashOperationToolbar query={query} operationType={operationType} resultCount={filtered.length} onQuery={setQuery} onType={setOperationType} onExport={() => void exportCsv()} />{register}</>}
  inspector={selected ? <CashOperationInspector operation={selected} currencyCode={currencyCode} /> : <CashInspectorEmpty />}
/>
```

Show actor, source, and running balance only when present. Increment a local reload nonce on retry.

- [ ] **Step 4: Regenerate, verify, and commit**

```bash
cd packages/i18n && bun run gen && bun test src/messages.test.ts
cd ../../src/AFK4.Operator.App.Web && bun test src/cash/cashTerminalModel.test.ts src/cash/CashOperationsLedger.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/cash src/AFK4.Operator.App.Web/src/styles/21-cash.css locales packages/i18n/src/messages.ts
git commit -m "feat(operator-cash): redesign cash operations register"
```

---

### Task 5: Rebuild Receipts And Preview Detail

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `locales/{ru,en,tg}.json`
- Regenerate: `packages/i18n/src/messages.ts`

**Interfaces:**
- Adds detail state `{ status: 'idle' | 'loading' | 'ready' | 'failed'; saleId: string; error: string | null }`.
- Adds preview GET routes `/api/pos/sales/{id}` and `/api/receipts/{id}`.

- [ ] **Step 1: Write failing receipt and preview tests**

```tsx
it('shows receipt lines and mixed payment in the inspector', async () => {
  renderReceipts();
  fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));
  const inspector = await screen.findByLabelText('Детали выбранной записи');
  expect(inspector).toHaveTextContent('Cola 0.5');
  expect(inspector).toHaveTextContent('Наличные');
  expect(inspector).toHaveTextContent('12 с.');
});

it('shows retry instead of a false zero-value detail', async () => {
  getSale.mockRejectedValueOnce(new Error('detail failed'));
  renderReceipts();
  fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));
  expect(await screen.findByText('Не удалось загрузить детали чека')).toBeInTheDocument();
  expect(screen.queryByText(/^0 с\.$/)).toBeNull();
});

it('returns matching preview sale and receipt detail', async () => {
  const sale = await (await devMockFetch('https://x/api/pos/sales/ps-06')).json();
  expect(sale).toMatchObject({ posSaleId: 'ps-06', state: 'paid' });
  const receipt = await (await devMockFetch(`https://x/api/receipts/${sale.latestReceipt.receiptId}`)).json();
  expect(receipt.total).toEqual(sale.total);
});
```

- [ ] **Step 2: Verify red**

```bash
bun test src/cash/CashReceiptsLedger.test.tsx src/devMockBackend.test.ts
```

Expected: FAIL because detail is inline, errors are toast-only, and preview GET detail is empty.

- [ ] **Step 3: Implement stale-safe detail loading and master-detail UI**

```ts
const detailRequest = useRef(0);
const loadSaleDetail = async (saleId: string) => {
  const request = ++detailRequest.current;
  setDetailState({ status: 'loading', saleId, error: null });
  try {
    const sale = await built.pos.getSale(saleId);
    const receiptId = readString(readRecord(sale, 'latestReceipt'), 'receiptId');
    const receipt = receiptId ? await built.pos.getReceipt(receiptId) : null;
    if (request !== detailRequest.current) return;
    setSaleDetail(sale); setReceiptDetail(receipt);
    setDetailState({ status: 'ready', saleId, error: null });
  } catch (error) {
    if (request !== detailRequest.current) return;
    setDetailState({ status: 'failed', saleId, error: projectOperatorError(error, t).detail });
  }
};
```

Render lines, line totals, available payment parts, receipt state, and existing print/export/refund actions in the inspector. Preserve the critical reason and idempotent refund command.

- [ ] **Step 4: Make preview report/detail share one fixture source**

```ts
function posSales() {
  return [{
    posSaleId: 'ps-06', state: 'paid', total: money(1200), createdAtUtc: minutesAgoUtc(10),
    lines: [{ productId: 'prod-cola', productName: 'Cola 0.5', quantity: 1, unitPrice: money(1200), lineTotal: money(1200) }],
    payments: [{ method: 'cash', amount: money(1200) }],
    latestReceipt: { receiptId: 'rc-06', receiptNumber: '1048', total: money(1200) }
  }];
}
```

Generate report rows from this collection and route exact GETs before mutation routes.

- [ ] **Step 5: Regenerate, verify, and commit**

```bash
cd packages/i18n && bun run gen && bun test src/messages.test.ts
cd ../../src/AFK4.Operator.App.Web
bun test src/cash/CashReceiptsLedger.test.tsx src/devMockBackend.test.ts
bunx tsc -b --pretty false
cd ../../..
git add src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.test.tsx src/AFK4.Operator.App.Web/src/devMockBackend.ts src/AFK4.Operator.App.Web/src/devMockBackend.test.ts src/AFK4.Operator.App.Web/src/styles/21-cash.css locales packages/i18n/src/messages.ts
git commit -m "feat(operator-cash): rebuild receipt register"
```

---

### Task 6: Rename Review To Approvals And Rebuild Audit

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/ReviewWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/styles/18-review.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `locales/{ru,en,tg}.json`
- Regenerate: `packages/i18n/src/messages.ts`

- [ ] **Step 1: Write failing approval/audit tests**

```tsx
it('renames the segment to Согласования', () => {
  renderJournal(['billing.money_action.approve']);
  expect(screen.getByRole('tab', { name: 'Согласования' })).toBeInTheDocument();
  expect(screen.queryByRole('tab', { name: 'Проверка' })).toBeNull();
});

it('selects a pending request into a risk inspector', async () => {
  renderReview({ requests: [pendingRequest] });
  fireEvent.click(await screen.findByRole('row', { name: /Возврат.*120/ }));
  const inspector = screen.getByLabelText('Детали выбранной записи');
  expect(inspector).toHaveTextContent('Истекает');
  expect(inspector).toHaveTextContent('Ошибочный чек');
});

it('preserves rejection reason after backend failure', async () => {
  reject.mockRejectedValueOnce(new Error('network'));
  renderReview({ requests: [pendingRequest] });
  openRejectFor(pendingRequest);
  fireEvent.change(screen.getByLabelText('Причина отклонения'), { target: { value: 'Нет подтверждения клиента' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отклонение' }));
  expect(await screen.findByDisplayValue('Нет подтверждения клиента')).toBeInTheDocument();
});
```

- [ ] **Step 2: Verify red**

```bash
bun test src/cash/CashJournalWorkspace.test.tsx src/ReviewWorkspace.test.tsx src/devMockBackend.test.ts
```

Expected: FAIL for old copy, missing test component, inline queue, and empty preview data.

- [ ] **Step 3: Implement queue/history/audit master-detail views**

```tsx
const reviewTabs = [
  { id: 'queue', label: t('op.review.tabQueue') },
  { id: 'history', label: t('op.review.tabHistory') },
  { id: 'audit', label: t('op.review.tabAudit') }
] as const;

<CashTerminalSplit
  inspectorOpen={selectedRequest !== null}
  onCloseInspector={() => setSelectedRequestId('')}
  register={<ApprovalRegister requests={requests} selectedId={selectedRequestId} onSelect={setSelectedRequestId} staffNames={staffNames} />}
  inspector={selectedRequest ? <ApprovalInspector request={selectedRequest} onApprove={() => void approveRequest(selectedRequest)} onReject={() => setRejectingId(selectedRequest.moneyActionRequestId)} /> : <CashInspectorEmpty />}
/>
```

Use filtered audit records for decision history; do not invent local history. Preserve the rejection reason until success or explicit cancel.

- [ ] **Step 4: Add mutable preview pending/audit fixtures**

Return one pending refund request and several audit records. Approve/reject must remove the pending request and append a decision record so the preview visibly confirms the workflow.

- [ ] **Step 5: Regenerate, verify, and commit**

```bash
cd packages/i18n && bun run gen && bun test src/messages.test.ts
cd ../../src/AFK4.Operator.App.Web
bun test src/cash/CashJournalWorkspace.test.tsx src/ReviewWorkspace.test.tsx src/devMockBackend.test.ts
bun test src/App.test.tsx --test-name-pattern 'money-action|audit|cash journal'
cd ../../..
git add src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx src/AFK4.Operator.App.Web/src/ReviewWorkspace.test.tsx src/AFK4.Operator.App.Web/src/devMockBackend.ts src/AFK4.Operator.App.Web/src/devMockBackend.test.ts src/AFK4.Operator.App.Web/src/styles/18-review.css src/AFK4.Operator.App.Web/src/styles/21-cash.css locales packages/i18n/src/messages.ts
git commit -m "feat(operator-cash): redesign approvals and audit"
```

---

### Task 7: Responsive Integration, Rendered QA, And Completion Gate

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/18-review.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Move after completion: design and plan from `docs/superpowers/` to `docs/archive/superpowers/` and update both README indexes.

- [ ] **Step 1: Write failing App/CSS guards**

```tsx
it('opens journal receipts for receipt-only staff', async () => {
  restoreSession({ permissions: ['receipts.view'] });
  renderApp();
  fireEvent.click(await screen.findByRole('button', { name: 'Касса' }));
  fireEvent.click(screen.getByRole('tab', { name: 'Журнал кассы' }));
  expect(await screen.findByRole('tab', { name: 'Чеки' })).toHaveAttribute('aria-selected', 'true');
});
```

```ts
expect(cashCss).toContain(".cash-register-row[aria-selected='true']");
expect(cashCss).toContain('@media (max-width: 1180px)');
expect(cashCss).toContain('@media (prefers-reduced-motion: reduce)');
expect(cashCss).not.toMatch(/\.cash-register-row[^}]*transform:\s*translate/);
```

- [ ] **Step 2: Verify red, then implement responsive drawer**

```bash
bun test src/styles/qaContrast.test.ts
bun test src/App.test.tsx --test-name-pattern 'receipt-only|cash journal'
```

Use:

```css
@media (max-width: 1180px) {
  .cash-terminal-split { grid-template-columns:minmax(0, 1fr); position:relative; }
  .cash-terminal-inspector { position:absolute; inset:0 0 0 auto; width:min(420px, 92%); z-index:4; box-shadow:var(--shadow-lg); transform:translateX(100%); pointer-events:none; }
  .cash-terminal-split.inspector-open .cash-terminal-inspector { transform:translateX(0); pointer-events:auto; }
  .cash-inspector-close { display:inline-flex; }
}
@media (prefers-reduced-motion: reduce) {
  .cash-terminal-inspector, .cash-register-row { transition:none; }
}
```

Escape closes the drawer or confirmation but never cancels a pending command.

- [ ] **Step 3: Run focused integration verification**

```bash
bun test src/cash src/ReviewWorkspace.test.tsx src/devMockBackend.test.ts src/operatorRealtime.test.ts src/styles/qaContrast.test.ts
bun test src/App.test.tsx --test-name-pattern 'shift|cash|receipt|money-action|audit'
bunx tsc -b --pretty false
```

Expected: all focused tests and typecheck PASS.

- [ ] **Step 4: Run preview QA on port 5175**

Start `bunx vite --host 127.0.0.1 --port 5175`. With Playwright and `/usr/sbin/chromium-browser`, sign in using any non-empty preview credentials and capture outside the repo:

```text
/tmp/afk4-cash-shift-dark-1440.png
/tmp/afk4-cash-operations-dark-1440.png
/tmp/afk4-cash-receipts-dark-1440.png
/tmp/afk4-cash-approvals-dark-1440.png
/tmp/afk4-cash-shift-light-1280.png
/tmp/afk4-cash-receipts-light-1280.png
```

Exercise shift-history selection, operation filtering, receipt selection, refund-confirm/cancel, approval selection, audit filters, keyboard rows, drawer close, and console health.

- [ ] **Step 5: Inspect screenshots and close the fidelity ledger**

Use `view_image` on every screenshot. Check metric hierarchy, 64/36 balance, stable rows, amount alignment, toolbar typography, 1280 overflow, dark/light contrast, and loading/empty/error geometry. Fix all actionable drift and rerun the affected screenshot.

- [ ] **Step 6: Run the full completion gate**

```bash
cd src/AFK4.Operator.App.Web
bun run test
bun run build
cd ../../..
git diff --check
git status --short --branch
```

Expected: 753 or more tests PASS, production build succeeds, and only intentional files differ.

- [ ] **Step 7: Update durable project state, archive plan/spec, and commit**

Record the redesigned terminal, current test count, build, rendered sizes/themes, and remaining gaps in the compact progress file. Move the completed plan/spec to archive and remove active index entries.

```bash
git diff --check
git add src/AFK4.Operator.App.Web/src/App.test.tsx src/AFK4.Operator.App.Web/src/styles docs locales packages/i18n/src/messages.ts
git commit -m "docs(operator): record cash terminal redesign"
git status --short --branch
git log --oneline --decorate origin/main..HEAD
```

Expected: clean, coherent, unpushed topic branch ready for review. Do not push or merge without a later explicit request.
